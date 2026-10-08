using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
namespace DispatchEngine.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase {
    private readonly IConfiguration _config;
    public AdminController(IConfiguration config) { _config = config; }
    [HttpGet("stats")] public async Task<IActionResult> Stats() { using var con = new SqlConnection(_config.GetConnectionString("Default")); var stats = await con.QueryFirstOrDefaultAsync("EXEC sp_AdminStats"); return Ok(stats); }
    [HttpGet("users")] public async Task<IActionResult> Users() { using var con = new SqlConnection(_config.GetConnectionString("Default")); return Ok(await con.QueryAsync("EXEC sp_GetAllUsers")); }
    [HttpGet("orders")] public async Task<IActionResult> Orders() { using var con = new SqlConnection(_config.GetConnectionString("Default")); return Ok(await con.QueryAsync("EXEC sp_GetAllOrders")); }
    [HttpGet("riders")] public async Task<IActionResult> Riders() { using var con = new SqlConnection(_config.GetConnectionString("Default")); return Ok(await con.QueryAsync("EXEC sp_GetAllRiders")); }
    [HttpGet("wallets")] public async Task<IActionResult> Wallets() { using var con = new SqlConnection(_config.GetConnectionString("Default")); return Ok(await con.QueryAsync("SELECT w.*, u.Name FROM Wallets w JOIN Users u ON w.UserId=u.Id")); }
    [HttpGet("transactions")] public async Task<IActionResult> Transactions() { using var con = new SqlConnection(_config.GetConnectionString("Default")); return Ok(await con.QueryAsync("SELECT t.*, u.Name FROM WalletTransactions t JOIN Wallets w ON t.WalletId=w.Id JOIN Users u ON w.UserId=u.Id ORDER BY t.CreatedAt DESC")); }
}
