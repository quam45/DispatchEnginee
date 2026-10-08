using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
namespace DispatchEngine.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
public class WalletController : ControllerBase {
    private readonly IConfiguration _config;
    public WalletController(IConfiguration config) { _config = config; }

    [HttpGet("balance")]
    public async Task<IActionResult> Balance([FromQuery] int userId) {
        using var con = new SqlConnection(_config.GetConnectionString("Default"));
        var wallet = await con.QueryFirstOrDefaultAsync("EXEC sp_GetWalletByUserId @UserId", new { UserId = userId });
        return Ok(wallet);
    }
    [HttpPost("fund")]
    public async Task<IActionResult> Fund([FromQuery] int userId, [FromQuery] decimal amount) {
        using var con = new SqlConnection(_config.GetConnectionString("Default"));
        var wallet = await con.QueryFirstOrDefaultAsync("EXEC sp_FundWallet @UserId, @Amount", new { UserId = userId, Amount = amount });
        if(wallet==null) return NotFound("Wallet not found");
        return Ok(new { message = $"Funded ₦{amount}", balance = wallet.Balance, reference = "FUND_"+Guid.NewGuid().ToString("N").Substring(0,8).ToUpper() });
    }
    [HttpPost("pay-order/{orderId}")]
    public async Task<IActionResult> PayOrder(int orderId, [FromQuery] int userId) {
        using var con = new SqlConnection(_config.GetConnectionString("Default"));
        try { var res = await con.QueryFirstOrDefaultAsync("EXEC sp_PayOrder @OrderId, @UserId", new { OrderId = orderId, UserId = userId }); if(res==null) return NotFound("Order not found"); return Ok(new { message = $"Paid ₦{res.PaidAmount} for {res.OrderRef}", reference = "PAY_"+res.OrderRef }); }
        catch(Exception ex) { return BadRequest(ex.Message); }
    }
    [HttpGet("transactions")]
    public async Task<IActionResult> Transactions([FromQuery] int userId) {
        using var con = new SqlConnection(_config.GetConnectionString("Default"));
        var tx = await con.QueryAsync("SELECT t.* FROM WalletTransactions t JOIN Wallets w ON t.WalletId=w.Id WHERE w.UserId=@UserId ORDER BY t.CreatedAt DESC", new { UserId = userId });
        return Ok(tx);
    }
}
