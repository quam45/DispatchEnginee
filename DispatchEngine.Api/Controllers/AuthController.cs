using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using DispatchEngine.Api.Services;
namespace DispatchEngine.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase {
    private readonly IConfiguration _config;
    private readonly AuthService _auth;
    public AuthController(IConfiguration config, AuthService auth) { _config = config; _auth = auth; }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto) {
        using var con = new SqlConnection(_config.GetConnectionString("Default"));
        var exists = await con.QueryFirstOrDefaultAsync("SELECT Id FROM Users WHERE Email=@Email", new { dto.Email });
        if (exists!= null) return BadRequest("Email already exists");
        var hash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        var id = await con.ExecuteScalarAsync<int>("INSERT INTO Users (Name, Phone, Email, PasswordHash, Role) VALUES (@Name, @Phone, @Email, @hash, @Role); SELECT CAST(SCOPE_IDENTITY() as int)", new { dto.Name, dto.Phone, dto.Email, hash, dto.Role });
        await con.ExecuteAsync("INSERT INTO Wallets (UserId, Balance) VALUES (@UserId, 0)", new { UserId = id });
        var token = _auth.GenerateToken(id, dto.Email, dto.Role);
        return Ok(new { id, email = dto.Email, role = dto.Role, token });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto) {
        using var con = new SqlConnection(_config.GetConnectionString("Default"));
        var user = await con.QueryFirstOrDefaultAsync<dynamic>("SELECT * FROM Users WHERE Email=@Email", new { dto.Email });
        if (user == null) return Unauthorized("Invalid email");
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, (string)user.PasswordHash)) return Unauthorized("Invalid password");
        var token = _auth.GenerateToken((int)user.Id, (string)user.Email, (string)user.Role);
        return Ok(new { userId = user.Id, email = user.Email, role = user.Role, token });
    }
}
public record RegisterDto(string Name, string Phone, string Email, string Password, string Role);
public record LoginDto(string Email, string Password);
