using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Dapper;
using DispatchEngine.Api.Errors;
using DispatchEngine.Api.Hubs;
using DispatchEngine.Api.Services;
using DispatchEngine.Infrastructure;
using Microsoft.AspNetCore.SignalR;

namespace DispatchEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RidersController : ControllerBase
{
    private readonly IDbConnectionFactory _db;
    private readonly IHubContext<TrackingHub> _hub;
    public RidersController(IDbConnectionFactory db, IHubContext<TrackingHub> hub) { _db = db; _hub = hub; }

    [HttpGet]
    [Authorize(Roles = Roles.Admin)] // includes every rider's phone number and live position
    public async Task<IActionResult> GetAll()
    {
        using var con = _db.CreateConnection();
        return Ok(await con.QueryAsync("dbo.sp_GetAllRiders", commandType: CommandType.StoredProcedure));
    }

    // 5 location updates per 10 seconds *per rider* (see the "rider" policy in Program.cs).
    // The rider is whoever the token belongs to, so one rider can't move another on the map.
    [EnableRateLimiting("rider")]
    [Authorize(Roles = Roles.Rider)]
    [HttpPost("update-location")]
    public async Task<IActionResult> UpdateLocation([FromQuery, Range(-90, 90)] double lat, [FromQuery, Range(-180, 180)] double lng)
    {
        if (User.GetRiderId() is not int riderId) return this.Error(403, "NOT_A_RIDER", "Your account has no rider profile.");
        using var con = _db.CreateConnection();
        var rider = await con.QuerySingleAsync("dbo.sp_UpdateRiderLocation", new { RiderId = riderId, Lat = lat, Lng = lng }, commandType: CommandType.StoredProcedure);
        await _hub.Clients.All.SendAsync("RiderLocationUpdated", riderId, lat, lng);
        return Ok(rider);
    }
}
