using System.Security.Claims;
namespace DispatchEngine.Api.Services;

public static class Roles {
    public const string Customer = "CUSTOMER";
    public const string Rider = "RIDER";
    public const string Admin = "ADMIN";
}

// Identity always comes from the signed JWT, never from a userId/riderId in the request,
// so a caller can't act on someone else's account by changing a number.
public static class CurrentUser {
    public const string RiderIdClaim = "rider_id";

    public static int GetUserId(this ClaimsPrincipal user) => int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static int? GetRiderId(this ClaimsPrincipal user) => int.TryParse(user.FindFirstValue(RiderIdClaim), out var id) ? id : null;
    public static bool CanAccessUser(this ClaimsPrincipal user, int userId) => user.GetUserId() == userId || user.IsInRole(Roles.Admin);
}
