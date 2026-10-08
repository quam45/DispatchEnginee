using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using DispatchEngine.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
namespace DispatchEngine.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase {
    private readonly IConfiguration _config;
    private readonly IHubContext<TrackingHub> _hub;
    public OrdersController(IConfiguration config, IHubContext<TrackingHub> hub) { _config = config; _hub = hub; }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderDto dto, [FromHeader(Name="X-Idempotency-Key")] string? idemKey) {
        using var con = new SqlConnection(_config.GetConnectionString("Default"));
        if(!string.IsNullOrEmpty(idemKey)) {
            var existing = await con.QueryFirstOrDefaultAsync("EXEC sp_CheckIdempotency @Key", new { Key = idemKey });
            if(existing!=null) return Ok(await con.QueryFirstOrDefaultAsync("EXEC sp_GetOrderById @Id", new { Id = (int)existing.OrderId })); // return same order, no duplicate
        }
        var distance = Haversine(dto.PickupLat, dto.PickupLng, dto.DropoffLat, dto.DropoffLng);
        var price = Math.Round(300 + (distance * 150), 2);
        var reference = "DIS_" + Guid.NewGuid().ToString("N").Substring(0,8).ToUpper();
        var otp = new Random().Next(100000, 999999).ToString();
        var order = await con.QueryFirstOrDefaultAsync("EXEC sp_CreateOrder @Reference, @CustomerId, @PickupLat, @PickupLng, @PickupAddress, @DropoffLat, @DropoffLng, @DropoffAddress, @Price, @DistanceKm, @DeliveryOtp",
            new { Reference = reference, CustomerId = dto.CustomerId, PickupLat = dto.PickupLat, PickupLng = dto.PickupLng, PickupAddress = dto.PickupAddress, DropoffLat = dto.DropoffLat, DropoffLng = dto.DropoffLng, DropoffAddress = dto.DropoffAddress, Price = price, DistanceKm = distance, DeliveryOtp = otp });
        if(order==null) return StatusCode(500, "Order could not be created");
        if(!string.IsNullOrEmpty(idemKey)) await con.ExecuteAsync("EXEC sp_SaveIdempotency @Key, @OrderId", new { Key = idemKey, OrderId = (int)order.Id });
        await _hub.Clients.All.SendAsync("OrderStatus", reference, "PENDING");
        return Ok(order);
    }

    [HttpPost("{id}/accept")]
    public async Task<IActionResult> Accept(int id, [FromQuery] int riderId) {
        using var con = new SqlConnection(_config.GetConnectionString("Default"));
        var order = await con.QueryFirstOrDefaultAsync("EXEC sp_AcceptOrder @OrderId, @RiderId", new { OrderId = id, RiderId = riderId });
        if(order==null) return NotFound();
        await _hub.Clients.All.SendAsync("OrderStatus", (string)order.Reference, "ACCEPTED");
        return Ok(order);
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> Complete(int id, [FromQuery] string otp) {
        using var con = new SqlConnection(_config.GetConnectionString("Default"));
        try {
            var order = await con.QueryFirstOrDefaultAsync("EXEC sp_CompleteOrderWithOtp @OrderId, @Otp", new { OrderId = id, Otp = otp });
            if(order==null) return NotFound();
            await _hub.Clients.All.SendAsync("OrderStatus", (string)order.Reference, "DELIVERED");
            return Ok(order);
        } catch(Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() {
        using var con = new SqlConnection(_config.GetConnectionString("Default"));
        return Ok(await con.QueryAsync("EXEC sp_GetAllOrdersList"));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id) {
        using var con = new SqlConnection(_config.GetConnectionString("Default"));
        return Ok(await con.QueryFirstOrDefaultAsync("EXEC sp_GetOrderById @Id", new { Id = id }));
    }

    [HttpGet("nearest")]
    public async Task<IActionResult> Nearest([FromQuery] double lat, [FromQuery] double lng) {
        using var con = new SqlConnection(_config.GetConnectionString("Default"));
        return Ok(await con.QueryAsync("EXEC sp_GetNearestRiders @Lat, @Lng", new { Lat = lat, Lng = lng }));
    }

    private double Haversine(double lat1, double lon1, double lat2, double lon2) {
        var R=6371; var dLat=(lat2-lat1)*Math.PI/180; var dLon=(lon2-lon1)*Math.PI/180;
        var a=Math.Sin(dLat/2)*Math.Sin(dLat/2)+Math.Cos(lat1*Math.PI/180)*Math.Cos(lat2*Math.PI/180)*Math.Sin(dLon/2)*Math.Sin(dLon/2);
        return R*2*Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
    }
}
public record CreateOrderDto(double PickupLat, double PickupLng, string PickupAddress, double DropoffLat, double DropoffLng, string DropoffAddress, int CustomerId);
