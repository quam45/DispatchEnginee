using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting; // <-- 1. Add this
using Dapper;
using Microsoft.Data.SqlClient;
using DispatchEngine.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace DispatchEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RidersController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IHubContext<TrackingHub> _hub;
    public RidersController(IConfiguration config, IHubContext<TrackingHub> hub) { _config = config; _hub = hub; }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        using var con = new SqlConnection(_config.GetConnectionString("Default"));
        return Ok(await con.QueryAsync("EXEC sp_GetAllRiders"));
    }

    // This line = "Only 5 location updates per 10 seconds allowed"
    [EnableRateLimiting("rider")] // <-- 2. Add this
    [HttpPost("update-location")]
    public async Task<IActionResult> UpdateLocation([FromQuery] int riderId, [FromQuery] double lat, [FromQuery] double lng)
    {
        using var con = new SqlConnection(_config.GetConnectionString("Default"));
        var rider = await con.QueryFirstOrDefaultAsync("EXEC sp_UpdateRiderLocation @RiderId, @Lat, @Lng", new { RiderId = riderId, Lat = lat, Lng = lng });
        if(rider==null) return NotFound("Rider not found");
        await _hub.Clients.All.SendAsync("RiderLocationUpdated", riderId, lat, lng);
        return Ok(rider);
    }
}