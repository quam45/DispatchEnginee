using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dapper;
using DispatchEngine.Api.Errors;
using DispatchEngine.Api.Services;
using DispatchEngine.Core;
using DispatchEngine.Infrastructure;
namespace DispatchEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WalletsController : ControllerBase
{
    private readonly IDbConnectionFactory _db;
    private readonly IPaystackService _paystack;
    private readonly ILogger<WalletsController> _logger;

    public WalletsController(IDbConnectionFactory db, IPaystackService paystack, ILogger<WalletsController> logger) { _db = db; _paystack = paystack; _logger = logger; }

    // GET api/Wallets/balance/1
    [HttpGet("balance/{userId:int}")]
    public async Task<IActionResult> GetBalance(int userId)
    {
        if (!User.CanAccessUser(userId)) return Forbid();
        using var conn = _db.CreateConnection();
        var wallet = await conn.QuerySingleOrDefaultAsync<Wallet>("dbo.sp_GetWalletBalance", new { UserId = userId }, commandType: CommandType.StoredProcedure);
        if (wallet == null) return this.Error(404, "WALLET_NOT_FOUND", "Wallet not found.");
        return Ok(wallet);
    }

    // POST api/Wallets/withdraw
    // Header: X-Idempotency-Key: <guid>
    // Body: { amount: 5000, accountNumber: "0123456789", bankCode: "058" }
    // Always withdraws from the caller's own wallet (user id comes from the token).
    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw([FromBody] WithdrawRequest req, [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
            return this.Error(400, "MISSING_IDEMPOTENCY_KEY", "An X-Idempotency-Key header (max 100 characters) is required for withdrawals.");

        using var conn = _db.CreateConnection();

        // 1. Debit (hold) the money and record a PENDING transaction. The procedure locks the wallet row,
        //    checks the balance and handles idempotency; business-rule failures surface as 4xx errors.
        var withdrawal = await conn.QuerySingleAsync<Withdrawal>("dbo.sp_CreateWithdrawal",
            new { UserId = User.GetUserId(), req.Amount, IdempotencyKey = idempotencyKey }, commandType: CommandType.StoredProcedure);

        // A retry of a withdrawal we've already started: report its state, never call Paystack a second time
        if (withdrawal.IsReplay)
            return Ok(new { withdrawal.Reference, withdrawal.Status, withdrawal.Amount, message = "Idempotent - returned existing transaction" });

        // 2. Call Paystack Transfer API (test mode)
        try
        {
            var paystackRef = await _paystack.InitiateTransfer(req.Amount, req.AccountNumber, req.BankCode, withdrawal.Reference);

            // 3. Update to SUCCESS
            await conn.ExecuteAsync("dbo.sp_UpdateTransactionStatus",
                new { withdrawal.Reference, NewStatus = "SUCCESS", PaystackReference = paystackRef }, commandType: CommandType.StoredProcedure);

            return Ok(new { withdrawal.Reference, status = "SUCCESS", paystackRef, amountDebited = req.Amount });
        }
        catch (PaystackTransferException ex)
        {
            // 4. Paystack definitely rejected the transfer, so no money left: refund the wallet
            _logger.LogWarning("Transfer {Reference} declined by Paystack: {Reason}", withdrawal.Reference, ex.Message);
            await conn.ExecuteAsync("dbo.sp_UpdateTransactionStatus",
                new { withdrawal.Reference, NewStatus = "FAILED", PaystackReference = (string?)null }, commandType: CommandType.StoredProcedure);
            return this.Error(422, "TRANSFER_DECLINED", $"The transfer was declined and your wallet has been refunded. Reference: {withdrawal.Reference}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // A timeout or network error is ambiguous: Paystack may have sent the money. Refunding now could pay
            // the user twice, so the transaction stays PENDING (money held) until it is reconciled with
            // Paystack's transfer webhook or verify-transfer endpoint.
            _logger.LogError(ex, "Transfer {Reference} outcome unknown; left PENDING for reconciliation", withdrawal.Reference);
            return StatusCode(202, new { withdrawal.Reference, status = "PENDING", message = "Your withdrawal is processing. Check back shortly." });
        }
    }
}

public record WithdrawRequest(
    [Range(typeof(decimal), "100", "1000000", ErrorMessage = "Amount must be between 100 and 1,000,000.")] decimal Amount,
    [Required, RegularExpression(@"^\d{10}$", ErrorMessage = "AccountNumber must be a 10-digit NUBAN.")] string AccountNumber,
    [Required, RegularExpression(@"^\d{3,6}$", ErrorMessage = "BankCode must be 3-6 digits.")] string BankCode);
