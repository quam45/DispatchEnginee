using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace DispatchEngine.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase {
    private readonly IConfiguration _config;
    public UsersController(IConfiguration config) { _config = config; }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(int id) {
        using var con = new SqlConnection(_config.GetConnectionString("Default"));
        var user = await con.QueryFirstOrDefaultAsync("SELECT Id, Name, Phone, Email, Role FROM Users WHERE Id=@id", new { id });
        if (user == null) return NotFound();
        var wallet = await con.QueryFirstOrDefaultAsync("SELECT * FROM Wallets WHERE UserId=@id", new { id });
        var orders = await con.QueryAsync("SELECT * FROM Orders WHERE CustomerId=@id ORDER BY CreatedAt DESC", new { id });
        var orderCount = await con.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Orders WHERE CustomerId=@id", new { id });
        var spent = await con.ExecuteScalarAsync<decimal>("SELECT ISNULL(SUM(Price),0) FROM Orders WHERE CustomerId=@id AND Status='DELIVERED'", new { id });
        return Ok(new { user, wallet, stats = new { totalOrders = orderCount, totalSpent = spent }, recentOrders = orders });
    }

    [HttpGet("{id}/orders")]
    public async Task<IActionResult> UserOrders(int id) {
        using var con = new SqlConnection(_config.GetConnectionString("Default"));
        var data = await con.QueryAsync("SELECT * FROM Orders WHERE CustomerId=@id ORDER BY CreatedAt DESC", new { id });
        return Ok(data);
    }

    [HttpGet("{id}/wallet")]
    public async Task<IActionResult> UserWallet(int id) {
        using var con = new SqlConnection(_config.GetConnectionString("Default"));
        var wallet = await con.QueryFirstOrDefaultAsync("SELECT * FROM Wallets WHERE UserId=@id", new { id });
        var tx = await con.QueryAsync("SELECT t.* FROM WalletTransactions t JOIN Wallets w ON t.WalletId=w.Id WHERE w.UserId=@id ORDER BY t.CreatedAt DESC", new { id });
        return Ok(new { wallet, transactions = tx });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto) {
        using var con = new SqlConnection(_config.GetConnectionString("Default"));
        await con.ExecuteAsync("UPDATE Users SET Name=@Name, Phone=@Phone WHERE Id=@id", new { dto.Name, dto.Phone, id });
        return Ok(new { message = "Profile updated" });
    }
}
public record UpdateUserDto(string Name, string Phone);
