using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Dapper;
using DispatchEngine.Api.Errors;
using DispatchEngine.Api.Hubs;
using DispatchEngine.Api.Services;
using DispatchEngine.Core;
using DispatchEngine.Infrastructure;
using Microsoft.AspNetCore.SignalR;
namespace DispatchEngine.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase {
    private readonly IDbConnectionFactory _db;
    private readonly IHubContext<TrackingHub> _hub;
    public OrdersController(IDbConnectionFactory db, IHubContext<TrackingHub> hub) { _db = db; _hub = hub; }

    // Retrying with the same X-Idempotency-Key returns the original order instead of creating another
    [HttpPost]
    [Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> Create([FromBody] CreateOrderDto dto, [FromHeader(Name="X-Idempotency-Key")] string? idemKey) {
        if (idemKey is { Length: > 100 }) return this.Error(400, "INVALID_IDEMPOTENCY_KEY", "X-Idempotency-Key must be at most 100 characters.");
        var distance = Haversine(dto.PickupLat, dto.PickupLng, dto.DropoffLat, dto.DropoffLng);
        var price = Math.Round(300 + ((decimal)distance * 150), 2);
        var reference = "DIS_" + Guid.NewGuid().ToString("N").Substring(0,8).ToUpper();
        // Cryptographic RNG: System.Random is predictable, and this code is what proves delivery
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        using var con = _db.CreateConnection();
        var order = await con.QuerySingleAsync<Order>("dbo.sp_CreateOrder",
            new { Reference = reference, CustomerId = User.GetUserId(), dto.PickupLat, dto.PickupLng, dto.PickupAddress, dto.DropoffLat, dto.DropoffLng, dto.DropoffAddress, Price = price, DistanceKm = distance, DeliveryOtp = otp, IdempotencyKey = idemKey },
            commandType: CommandType.StoredProcedure);
        await _hub.Clients.All.SendAsync("OrderStatus", order.Reference, order.Status);
        return Ok(order);
    }

    // If two riders accept at once, exactly one wins; the other gets 409 ORDER_NOT_AVAILABLE (see sp_AcceptOrder)
    [HttpPost("{id:int}/accept")]
    [Authorize(Roles = Roles.Rider)]
    public async Task<IActionResult> Accept(int id) {
        if (User.GetRiderId() is not int riderId) return this.Error(403, "NOT_A_RIDER", "Your account has no rider profile.");
        using var con = _db.CreateConnection();
        var order = await con.QuerySingleAsync<Order>("dbo.sp_AcceptOrder", new { OrderId = id, RiderId = riderId }, commandType: CommandType.StoredProcedure);
        order.DeliveryOtp = null; // the rider gets the code from the customer at the door
        await _hub.Clients.All.SendAsync("OrderStatus", order.Reference, order.Status);
        return Ok(order);
    }

    [HttpPost("{id:int}/complete")]
    [Authorize(Roles = Roles.Rider)]
    [EnableRateLimiting("otp")] // a 6-digit code can be brute-forced if guesses are unlimited
    public async Task<IActionResult> Complete(int id, [FromBody] CompleteOrderDto dto) {
        if (User.GetRiderId() is not int riderId) return this.Error(403, "NOT_A_RIDER", "Your account has no rider profile.");
        using var con = _db.CreateConnection();
        var order = await con.QuerySingleAsync<Order>("dbo.sp_CompleteOrderWithOtp", new { OrderId = id, RiderId = riderId, dto.Otp }, commandType: CommandType.StoredProcedure);
        order.DeliveryOtp = null;
        await _hub.Clients.All.SendAsync("OrderStatus", order.Reference, order.Status);
        return Ok(order);
    }

    // Customers see their own orders. Riders see open (PENDING) jobs, or their own deliveries for any
    // other status. Admins see everything.
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) {
        status = status?.Trim().ToUpperInvariant();
        int? customerId = null, riderId = null;
        if (User.IsInRole(Roles.Customer)) customerId = User.GetUserId();
        else if (User.IsInRole(Roles.Rider)) {
            status ??= "PENDING";
            if (status != "PENDING") {
                if (User.GetRiderId() is not int rid) return this.Error(403, "NOT_A_RIDER", "Your account has no rider profile.");
                riderId = rid;
            }
        }
        else if (!User.IsInRole(Roles.Admin)) return Forbid();
        using var con = _db.CreateConnection();
        return Ok(await con.QueryPagedAsync<Order>("dbo.sp_GetAllOrdersList", new { CustomerId = customerId, RiderId = riderId, Status = status }, page, pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id) {
        using var con = _db.CreateConnection();
        var order = await con.QuerySingleOrDefaultAsync<Order>("dbo.sp_GetOrderById", new { Id = id }, commandType: CommandType.StoredProcedure);
        if (order == null) return this.Error(404, "ORDER_NOT_FOUND", "Order not found.");
        var isCustomer = User.IsInRole(Roles.Customer) && order.CustomerId == User.GetUserId();
        var riderId = User.GetRiderId();
        var isRider = User.IsInRole(Roles.Rider) && riderId != null && (order.RiderId == riderId || order.Status == "PENDING");
        if (!isCustomer && !isRider && !User.IsInRole(Roles.Admin)) return this.Error(404, "ORDER_NOT_FOUND", "Order not found.");
        if (!isCustomer) order.DeliveryOtp = null;
        return Ok(order);
    }

    [HttpGet("nearest")]
    public async Task<IActionResult> Nearest([FromQuery, Range(-90, 90)] double lat, [FromQuery, Range(-180, 180)] double lng) {
        using var con = _db.CreateConnection();
        return Ok(await con.QueryAsync("dbo.sp_GetNearestRiders", new { Lat = lat, Lng = lng }, commandType: CommandType.StoredProcedure));
    }

    private double Haversine(double lat1, double lon1, double lat2, double lon2) {
        var R=6371; var dLat=(lat2-lat1)*Math.PI/180; var dLon=(lon2-lon1)*Math.PI/180;
        var a=Math.Sin(dLat/2)*Math.Sin(dLat/2)+Math.Cos(lat1*Math.PI/180)*Math.Cos(lat2*Math.PI/180)*Math.Sin(dLon/2)*Math.Sin(dLon/2);
        return R*2*Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
    }
}
public record CreateOrderDto(
    [Range(-90, 90)] double PickupLat, [Range(-180, 180)] double PickupLng, [Required, StringLength(255)] string PickupAddress,
    [Range(-90, 90)] double DropoffLat, [Range(-180, 180)] double DropoffLng, [Required, StringLength(255)] string DropoffAddress);
public record CompleteOrderDto([Required, RegularExpression(@"^\d{6}$", ErrorMessage = "Otp must be 6 digits.")] string Otp);
