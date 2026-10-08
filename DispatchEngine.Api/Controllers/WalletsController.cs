using Microsoft.AspNetCore.Mvc;
using Dapper;
using System.Data;
using Microsoft.Data.SqlClient;
using DispatchEngine.Api.Services;
namespace DispatchEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WalletsController : ControllerBase
{
    private readonly string _conn;
    private readonly IPaystackService _paystack;

    public WalletsController(IConfiguration config, IPaystackService paystack) { _conn = config.GetConnectionString("Default")!; _paystack = paystack; }

    // GET api/Wallets/balance/1
    [HttpGet("balance/{userId}")]
    public async Task<IActionResult> GetBalance(int userId)
    {
        using var conn = new SqlConnection(_conn);
        var wallet = await conn.QueryFirstOrDefaultAsync("sp_GetWalletBalance", new { UserId = userId }, commandType: CommandType.StoredProcedure);
        if (wallet == null) return NotFound("Wallet not found");
        return Ok(wallet);
    }

    // POST api/Wallets/withdraw
    // Header: X-Idempotency-Key: <guid>
    // Body: { userId: 1, amount: 5000, accountNumber: "0123456789", bankCode: "058" }
    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw([FromBody] WithdrawRequest req)
    {
        var idempotencyKey = Request.Headers["X-Idempotency-Key"].ToString();
        if (string.IsNullOrEmpty(idempotencyKey))
            return BadRequest("Missing X-Idempotency-Key header - required for fintech safety");

        using var conn = new SqlConnection(_conn);
        await conn.OpenAsync();

        // 1. Call sp_CreateWithdrawal - it does UPDLOCK + idempotency check + debit
        var param = new DynamicParameters();
        param.Add("@UserId", req.UserId);
        param.Add("@Amount", req.Amount);
        param.Add("@IdempotencyKey", idempotencyKey);
        param.Add("@Reference", dbType: DbType.String, size: 50, direction: ParameterDirection.Output);
        param.Add("@Status", dbType: DbType.String, size: 50, direction: ParameterDirection.Output);

        await conn.ExecuteAsync("sp_CreateWithdrawal", param, commandType: CommandType.StoredProcedure);

        var reference = param.Get<string?>("@Reference");
        var status = param.Get<string?>("@Status");
        if (status == null || reference == null) return StatusCode(500, "Withdrawal could not be created");

        // Handle failures from SP
        if (status.StartsWith("FAILED"))
        {
            if (status == "FAILED_INSUFFICIENT_BALANCE") return BadRequest($"Insufficient balance. Current: check /balance/{req.UserId}");
            return BadRequest(status);
        }

        // If idempotency hit - return existing
        if (status != "PENDING")
        {
            return Ok(new { reference, status, message = "Idempotent - returned existing transaction" });
        }

        // 2. Call Paystack Transfer API (test mode)
        try
        {
            var paystackRef = await _paystack.InitiateTransfer(
                amount: req.Amount,
                accountNumber: req.AccountNumber,
                bankCode: req.BankCode,
                reference: reference
            );

            // 3. Update to SUCCESS
            await conn.ExecuteAsync("sp_UpdateTransactionStatus",
                new { Reference = reference, NewStatus = "SUCCESS", PaystackReference = paystackRef },
                commandType: CommandType.StoredProcedure);

            return Ok(new { reference, status = "SUCCESS", paystackRef, amountDebited = req.Amount });
        }
        catch (Exception ex)
        {
            // 4. If Paystack fails, refund wallet
            await conn.ExecuteAsync("sp_UpdateTransactionStatus",
                new { Reference = reference, NewStatus = "FAILED", PaystackReference = (string?)null },
                commandType: CommandType.StoredProcedure);

            return StatusCode(500, new { reference, status = "FAILED", error = ex.Message, note = "Wallet refunded" });
        }
    }
}

public record WithdrawRequest(int UserId, decimal Amount, string AccountNumber, string BankCode);
