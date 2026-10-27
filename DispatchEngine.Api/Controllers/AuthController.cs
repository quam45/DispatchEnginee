using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Dapper;
using DispatchEngine.Api.Errors;
using DispatchEngine.Api.Services;
using DispatchEngine.Core;
using DispatchEngine.Infrastructure;
namespace DispatchEngine.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")] // brute-force protection: limited attempts per IP
public class AuthController : ControllerBase {
    // Self sign-up can only create customers and riders. Admins are promoted in the database (Database/Seed/make_admin.sql).
    private static readonly string[] SelfServiceRoles = [Roles.Customer, Roles.Rider];

    private readonly IDbConnectionFactory _db;
    private readonly AuthService _auth;
    public AuthController(IDbConnectionFactory db, AuthService auth) { _db = db; _auth = auth; }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto) {
        var role = dto.Role.Trim().ToUpperInvariant();
        if (!SelfServiceRoles.Contains(role)) return this.Error(400, "INVALID_ROLE", "Role must be CUSTOMER or RIDER.");
        var email = dto.Email.Trim();
        using var con = _db.CreateConnection();
        // Duplicate email/phone is rejected by unique indexes inside the procedure (409 EMAIL_TAKEN / PHONE_TAKEN)
        var created = await con.QuerySingleAsync<User>("dbo.sp_RegisterUser",
            new { Name = dto.Name.Trim(), Phone = dto.Phone.Trim(), Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password), Role = role },
            commandType: CommandType.StoredProcedure);
        var token = _auth.GenerateToken(created.Id, email, role, created.RiderId);
        return Ok(new { id = created.Id, email, role, riderId = created.RiderId, token });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto) {
        using var con = _db.CreateConnection();
        var user = await con.QuerySingleOrDefaultAsync<User>("dbo.sp_GetUserByEmail", new { Email = dto.Email.Trim() }, commandType: CommandType.StoredProcedure);
        // One response for "no such email" and "wrong password", so login can't be used to find out which emails have accounts
        if (user == null || !AuthService.VerifyPassword(dto.Password, user.PasswordHash))
            return this.Error(401, "INVALID_CREDENTIALS", "Email or password is incorrect.");
        var token = _auth.GenerateToken(user.Id, user.Email, user.Role, user.RiderId);
        return Ok(new { userId = user.Id, email = user.Email, role = user.Role, riderId = user.RiderId, token });
    }
}
public record RegisterDto(
    [Required, StringLength(100, MinimumLength = 2)] string Name,
    [Required, Phone, StringLength(20)] string Phone,
    [Required, EmailAddress, StringLength(100)] string Email,
    [Required, StringLength(72, MinimumLength = 8)] string Password, // BCrypt ignores bytes past 72
    [Required] string Role);
public record LoginDto([Required, EmailAddress] string Email, [Required] string Password);
