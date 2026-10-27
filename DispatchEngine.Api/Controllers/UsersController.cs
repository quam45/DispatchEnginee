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
public class UsersController : ControllerBase {
    private readonly IDbConnectionFactory _db;
    public UsersController(IDbConnectionFactory db) { _db = db; }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUser(int id) {
        if (!User.CanAccessUser(id)) return Forbid();
        using var con = _db.CreateConnection();
        // One round trip, four result sets
        using var grid = await con.QueryMultipleAsync("dbo.sp_GetUserDashboard", new { UserId = id }, commandType: CommandType.StoredProcedure);
        var user = await grid.ReadSingleOrDefaultAsync();
        var wallet = await grid.ReadSingleOrDefaultAsync<Wallet>();
        var stats = await grid.ReadSingleAsync();
        var orders = await grid.ReadAsync<Order>();
        if (user == null) return this.Error(404, "USER_NOT_FOUND", "User not found.");
        return Ok(new { user, wallet, stats = new { totalOrders = (int)stats.TotalOrders, totalSpent = (decimal)stats.TotalSpent }, recentOrders = orders });
    }

    [HttpGet("{id:int}/orders")]
    public async Task<IActionResult> UserOrders(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) {
        if (!User.CanAccessUser(id)) return Forbid();
        using var con = _db.CreateConnection();
        return Ok(await con.QueryPagedAsync<Order>("dbo.sp_GetAllOrdersList", new { CustomerId = id }, page, pageSize));
    }

    [HttpGet("{id:int}/wallet")]
    public async Task<IActionResult> UserWallet(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) {
        if (!User.CanAccessUser(id)) return Forbid();
        using var con = _db.CreateConnection();
        var wallet = await con.QuerySingleOrDefaultAsync<Wallet>("dbo.sp_GetWalletBalance", new { UserId = id }, commandType: CommandType.StoredProcedure);
        if (wallet == null) return this.Error(404, "WALLET_NOT_FOUND", "Wallet not found.");
        var tx = await con.QueryPagedAsync<WalletTransaction>("dbo.sp_GetWalletTransactions", new { UserId = id }, page, pageSize);
        return Ok(new { wallet, transactions = tx });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto) {
        if (!User.CanAccessUser(id)) return Forbid();
        using var con = _db.CreateConnection();
        var user = await con.QuerySingleAsync("dbo.sp_UpdateUser", new { UserId = id, Name = dto.Name.Trim(), Phone = dto.Phone.Trim() }, commandType: CommandType.StoredProcedure);
        return Ok(user);
    }
}
public record UpdateUserDto([Required, StringLength(100, MinimumLength = 2)] string Name, [Required, Phone, StringLength(20)] string Phone);
