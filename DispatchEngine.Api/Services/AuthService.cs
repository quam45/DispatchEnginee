using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
namespace DispatchEngine.Api.Services;
public class AuthService {
    private readonly IConfiguration _config;
    public AuthService(IConfiguration config) { _config = config; }

    public string GenerateToken(int userId, string email, string role, int? riderId) {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim> {
            new(ClaimTypes.NameIdentifier, userId.ToString()), new(ClaimTypes.Email, email), new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (riderId is not null) claims.Add(new(CurrentUser.RiderIdClaim, riderId.Value.ToString()));
        // Short-lived: a leaked token is only useful until it expires. (Refresh tokens are the next step.)
        var expiry = DateTime.UtcNow.AddMinutes(_config.GetValue("Jwt:ExpiryMinutes", 60));
        var token = new JwtSecurityToken(issuer: _config["Jwt:Issuer"], audience: _config["Jwt:Audience"], claims: claims, expires: expiry, signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static bool VerifyPassword(string password, string? hash) {
        if (string.IsNullOrEmpty(hash)) return false;
        // A malformed hash (e.g. a hand-seeded placeholder) is a failed login, not a 500
        try { return BCrypt.Net.BCrypt.Verify(password, hash); } catch (BCrypt.Net.SaltParseException) { return false; }
    }
}
