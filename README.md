DispatchEngine - Bolt-Style Fintech Dispatch Backend
Senior-level .NET 8 API with Stored Procedures as source of truth, Real-time SignalR, and Fintech-grade safety patterns.

Admin Stats

Live Stats from SSMS: totalUsers: 4 | totalOrders: 6 | pendingOrders: 4 | deliveredOrders: 1 | walletBalance: ₦23,586.57 | idempotencyCount: 0

Tech Stack
API: ASP.NET Core 8, Dapper, SQL Server
Realtime: SignalR (TrackingHub) for live rider location
DB: DispatchEngine (18 tables) - 8 core + 7 Hangfire + Idempotency + Wallets
Safety: Rate Limiting, Idempotency, Background Jobs
19 Stored Procedures (Source of Truth)
All business logic lives in SQL:
sp_GetAllRiders, sp_UpdateRiderLocation, sp_CreateOrder, sp_GetOrders, sp_AdminStats, sp_CheckIdempotency, sp_SaveIdempotency, sp_GetWallets...

Senior Patterns Implemented
1. Rate Limiting - RidersController.cs
[EnableRateLimiting("rider")]
[HttpPost("update-location")]
Config in Program.cs:

builder.Services.AddRateLimiter(o => o.AddFixedWindowLimiter("rider", opt => {
    opt.PermitLimit = 5; // 5 requests
    opt.Window = TimeSpan.FromSeconds(10);
}));
app.UseRateLimiter();
Why: Prevents a buggy rider app from spamming 500 location updates/sec and crashing SignalR. Returns 429 Too Many Requests on 6th call.

2. Idempotency - Prevent Double Charge
Header: X-Idempotency-Key: <guid>

Table: IdempotencyKeys (KeyValue UNIQUE, OrderId, CreatedAt)

Flow:

Check if Key exists -> if yes, return existing order DIS_xxxx
If no -> Create order, save Key->OrderId
Second click with same key = no double wallet debit.
Fintech standard - same as Paystack/Flutterwave.

3. Hangfire - Auto Requeue Stale Orders
Tables: Job, State, JobQueue, Server, JobParameter... (7 tables auto-created)

Job runs every 1 min:

SELECT * FROM Orders WHERE Status='PENDING' AND CreatedAt < DATEADD(minute, -5, GETDATE())
Re-pushes to riders via SignalR or alerts admin. Currently 4 pending orders will be auto-handled.

How to Run
dotnet restore
dotnet run --urls "https://localhost:5141"
Test endpoints:

GET /api/Admin/stats -> returns screenshot above
POST /api/Riders/update-location?riderId=1&lat=6.5&lng=3.3
POST /api/Orders + Header X-Idempotency-Key: test123
Schema (Actual)
Orders: Id, Reference (DIS_xxxx), CustomerId, RiderId, Status (PENDING/ACCEPTED/DELIVERED), PickupLat, PickupLng, PickupAddress
Wallets: Id, UserId, Balance, UpdatedAt
IdempotencyKeys: Id, KeyValue, OrderId

Author
Built by alagba quam - Lagos, Nigeria. From rider-location spam bug to senior fintech patterns.
