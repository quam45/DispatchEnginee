# DispatchEngine — Bolt-Style Dispatch & Wallet Backend

A delivery-dispatch API for a Lagos logistics business: customers book deliveries, nearby riders accept them, the rider proves delivery with the customer's one-time code, and riders withdraw earnings to their bank account through Paystack.

The interesting part isn't the CRUD. It's what happens when things go wrong at the same time: two riders accepting one order, a client retrying a withdrawal, or Paystack timing out mid-transfer.

**Stack:** ASP.NET Core (.NET 10) · Dapper · SQL Server stored procedures · SignalR · Hangfire · JWT · BCrypt

---

## Architecture

```
Client ──HTTP──> Controllers ──Dapper──> Stored procedures ──> SQL Server
   ▲               │  JWT auth, validation, rate limiting
   │               │  IDbConnectionFactory (one place for the connection string)
   │               └─> PaystackService (transfers)
   └──SignalR──── TrackingHub  <── pushed by controllers after the DB commits
                  Hangfire     ──> every minute: flag orders still PENDING after 5 min
```

| Project | Responsibility |
|---|---|
| `DispatchEngine.Api` | Controllers, auth, error handling, rate limiting, SignalR hub, Hangfire job |
| `DispatchEngine.Core` | Models / DTOs (`Order`, `Wallet`, `Withdrawal`, `PagedResult<T>`) |
| `DispatchEngine.Infrastructure` | `SqlConnectionFactory`, Dapper paging helper |
| `Database/` | Every table, constraint, index and stored procedure, deployable with one script |

## Run it locally

Prerequisites: .NET 10 SDK, SQL Server (Express is fine), `sqlcmd`.

```powershell
# 1. Create/upgrade the database. Every script is idempotent, so it's safe to re-run.
.\Database\deploy.ps1                       # defaults: localhost\SQLEXPRESS, DispatchEngine

# 2. JWT signing key: kept out of Git, stored in user-secrets
dotnet user-secrets set "Jwt:Key" "<random string, 32+ bytes>" --project DispatchEngine.Api

# 3. Run
dotnet run --project DispatchEngine.Api --launch-profile http
```

Swagger is at `http://localhost:5141/swagger`. Register, copy the `token`, and click **Authorize**. `DispatchEngine.Api/DispatchEngine.Api.http` has every request ready to send.

To get an admin: register normally, then run
`sqlcmd -S localhost\SQLEXPRESS -E -d DispatchEngine -I -v Email="you@example.com" -i Database\Seed\make_admin.sql`
and log in again.

### Configuration

| Key | Where | Notes |
|---|---|---|
| `ConnectionStrings:Default` | appsettings / `ConnectionStrings__Default` env var | |
| `Jwt:Key` | user-secrets / `Jwt__Key` env var | Required. The app refuses to start without a 32+ byte key |
| `Jwt:ExpiryMinutes` | appsettings | Default 60 |

## Endpoints

| | Endpoint | Who |
|---|---|---|
| Auth | `POST /api/Auth/register`, `POST /api/Auth/login` | anyone (rate limited) |
| Orders | `POST /api/Orders` (+ `X-Idempotency-Key`) | customer |
| | `GET /api/Orders?status=&page=&pageSize=` | customer: own · rider: open jobs / own · admin: all |
| | `GET /api/Orders/{id}`, `GET /api/Orders/nearest?lat=&lng=` | authenticated |
| | `POST /api/Orders/{id}/accept`, `POST /api/Orders/{id}/complete` | rider |
| Riders | `POST /api/Riders/update-location?lat=&lng=` | rider (5 / 10s each) |
| Wallets | `GET /api/Wallets/balance/{userId}`, `POST /api/Wallets/withdraw` | owner or admin |
| Users | `GET/PUT /api/Users/{id}`, `/orders`, `/wallet` | owner or admin |
| Admin | `/api/Admin/stats`, `users`, `orders`, `riders`, `wallets`, `transactions` | admin |
| Health | `GET /health` (liveness), `GET /health/ready` (SQL Server reachable) | anyone |

## Engineering decisions

### 1. Stored procedures are the source of truth
All business data access goes through stored procedures in `Database/StoredProcedures`, called with `CommandType.StoredProcedure` and named parameters, never concatenated SQL. Business rules that must hold regardless of the caller (state transitions, balance checks, idempotency) are enforced in the procedure *and* backed by constraints: `CK_Wallets_Balance >= 0`, `CK_Orders_Status`, unique email/phone.

Procedures report rule violations with `THROW (50000 + HTTP status), 'CODE: message'`. `GlobalExceptionHandler` turns that into the standard error response. A 409 from SQL becomes a 409 for the client, and no SQL text or stack trace ever leaks:

```json
{ "success": false, "error": { "code": "ORDER_NOT_AVAILABLE", "message": "This order has already been accepted or is no longer pending." }, "traceId": "00-..." }
```

### 2. Concurrency: two riders, one order
`sp_AcceptOrder` is a single conditional update:

```sql
UPDATE Orders SET RiderId = @RiderId, Status = 'ACCEPTED' WHERE Id = @OrderId AND Status = 'PENDING';
```

If two riders accept together, SQL Server makes the second update wait for the first one's row lock and then re-checks the `WHERE`. The order is no longer `PENDING`, so the second rider gets **409 ORDER_NOT_AVAILABLE** instead of silently overwriting the first. A filtered unique index (`UX_Orders_ActiveRider ON Orders(RiderId) WHERE Status = 'ACCEPTED'`) separately guarantees a rider can never hold two active deliveries (**409 RIDER_BUSY**).

Withdrawals lock the wallet row with `UPDLOCK`, so concurrent withdrawals queue up rather than all passing the balance check. Verified: five parallel ₦10,000 withdrawals against a ₦27,000 balance → exactly two succeed, balance ₦7,000.

### 3. Idempotency: retries never double-charge
`POST /api/Orders` and `POST /api/Wallets/withdraw` accept an `X-Idempotency-Key` header (required for withdrawals).

- Keys are scoped per user, so two users choosing the same key don't collide.
- The check and the write happen in one transaction. If two identical retries race, the unique index lets only one through, and the loser returns the winner's result.
- Reusing a withdrawal key with a different amount returns **422 IDEMPOTENCY_KEY_REUSED**.
- A replayed withdrawal never calls Paystack a second time.

### 4. Withdrawals: hold, transfer, settle
1. `sp_CreateWithdrawal` debits the wallet and records a `PENDING` transaction (money held).
2. Paystack transfer.
3. `sp_UpdateTransactionStatus` settles it. Only a `PENDING` transaction can change, so repeat settlement calls are no-ops and a refund can never happen twice.

Failure handling depends on what we actually know:
- **Paystack declined** (`PaystackTransferException`): no money moved → mark `FAILED`, refund.
- **Timeout / network error**: Paystack *may* have sent the money. Refunding now could pay the user twice, so the transaction stays `PENDING` (202 Accepted) for reconciliation.

### 5. Security
- JWT with issuer, audience, lifetime and signing-key validation; 60-minute tokens; key in user-secrets/env, never in Git.
- **Identity comes from the token, never the request body.** Withdrawals, location updates and order actions use the caller's own user/rider id, and role checks are on every controller.
- Self-registration can only create `CUSTOMER` or `RIDER` accounts.
- BCrypt password hashing; login returns the same error for unknown email and wrong password.
- The delivery OTP comes from a cryptographic RNG and is only visible to the order's customer. Guessing is rate limited (5/min per rider), and it's sent in the body, so it never ends up in URL logs.
- Rate limits are **per user/IP**, not global: `rider` 5/10s, `auth` 10/min, `otp` 5/min → **429**.
- The SignalR hub is server-to-client only; clients can't broadcast fake locations or statuses.

### 6. Pagination
List procedures take `@Page`/`@PageSize` (max 100), return the total count and the page in one round trip (`QueryMultipleAsync`), and use `OPTION (RECOMPILE)` so optional filters don't get stuck with a bad cached plan.

## Known limitations / roadmap

Next steps, roughly in order:

1. **Automated tests.** xUnit unit tests for pricing and validation. Integration tests against a real SQL Server (Testcontainers) for the concurrency and idempotency scenarios above.
2. **Refresh tokens** (rotating, stored hashed) and account lockout after repeated failed logins.
3. **Real Paystack** transfer client, plus a `POST /api/webhooks/paystack` endpoint with HMAC-SHA512 signature verification and idempotent event processing. This would also reconcile `PENDING` withdrawals.
4. **Configurable pricing rules** in a `PricingRules` table instead of `300 + 150/km` in the controller.
5. **Full delivery state machine** (PickedUp, InTransit, Cancelled, Failed) with a `DeliveryStatusHistory` table, plus an `AuditLogs` table.
6. **Layering.** Move data access from controllers into repositories/services in `Infrastructure`.
7. **Observability.** Serilog structured logs with correlation IDs; OpenTelemetry.
8. **Docker Compose** (API + SQL Server) and a **GitHub Actions** pipeline (build, test, docker build).
9. **Redis** for tracking-data caching and a distributed rate limiter (the current limiter is per instance); **RabbitMQ** for notifications.
10. Smaller items: send SignalR events to per-order groups instead of all clients; use `DATETIME2`/UTC everywhere; merge the `WalletTransactions` and `Transactions` ledgers; drop the `sp_` procedure prefix (SQL Server checks `master` first for `sp_` names).

## Author
Built by alagba quam - Lagos, Nigeria. From rider-location spam bug to senior fintech patterns.
