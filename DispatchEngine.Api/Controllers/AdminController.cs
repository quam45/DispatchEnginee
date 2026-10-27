using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dapper;
using DispatchEngine.Api.Services;
using DispatchEngine.Core;
using DispatchEngine.Infrastructure;
namespace DispatchEngine.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Admin)]
public class AdminController : ControllerBase {
    private readonly IDbConnectionFactory _db;
    public AdminController(IDbConnectionFactory db) { _db = db; }

    [HttpGet("stats")] public async Task<IActionResult> Stats() { using var con = _db.CreateConnection(); return Ok(await con.QuerySingleAsync("dbo.sp_AdminStats", commandType: CommandType.StoredProcedure)); }
    [HttpGet("users")] public async Task<IActionResult> Users([FromQuery] int page = 1, [FromQuery] int pageSize = 20) { using var con = _db.CreateConnection(); return Ok(await con.QueryPagedAsync<dynamic>("dbo.sp_GetAllUsers", null, page, pageSize)); }
    [HttpGet("orders")] public async Task<IActionResult> Orders([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) { using var con = _db.CreateConnection(); return Ok(await con.QueryPagedAsync<Order>("dbo.sp_GetAllOrdersList", new { Status = status }, page, pageSize)); }
    [HttpGet("riders")] public async Task<IActionResult> Riders() { using var con = _db.CreateConnection(); return Ok(await con.QueryAsync("dbo.sp_GetAllRiders", commandType: CommandType.StoredProcedure)); }
    [HttpGet("wallets")] public async Task<IActionResult> Wallets() { using var con = _db.CreateConnection(); return Ok(await con.QueryAsync("dbo.sp_GetAllWallets", commandType: CommandType.StoredProcedure)); }
    [HttpGet("transactions")] public async Task<IActionResult> Transactions([FromQuery] int page = 1, [FromQuery] int pageSize = 20) { using var con = _db.CreateConnection(); return Ok(await con.QueryPagedAsync<dynamic>("dbo.sp_GetWalletTransactions", null, page, pageSize)); }
}
