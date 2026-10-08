using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using DispatchEngine.Core;
namespace DispatchEngine.Infrastructure;
public class DispatchRepository {
    private readonly string _conn;
    public DispatchRepository(IConfiguration config) { _conn = config.GetConnectionString("Default")!; }
    public async Task<IEnumerable<Rider>> GetNearestRiders(double lat, double lng, int limit = 5) {
        using var con = new SqlConnection(_conn);
        var sql = "SELECT TOP (@limit) r.*, (6371 * acos(cos(radians(@lat)) * cos(radians(r.CurrentLat)) * cos(radians(r.CurrentLng) - radians(@lng)) + sin(radians(@lat)) * sin(radians(r.CurrentLat)))) AS DistanceKm FROM Riders r WHERE r.IsOnline = 1 ORDER BY DistanceKm";
        return await con.QueryAsync<Rider>(sql, new { lat, lng, limit });
    }
    public async Task<int> CreateOrder(Order o) {
        using var con = new SqlConnection(_conn);
        o.Reference = "DIS_" + Guid.NewGuid().ToString("N").Substring(0,8).ToUpper();
        o.DeliveryOtp = new Random().Next(100000, 999999).ToString();
        var dist = Calc(o.PickupLat, o.PickupLng, o.DropoffLat, o.DropoffLng);
        o.DistanceKm = dist;
        o.Price = 500 + (decimal)(dist * 100);
        var sql = "INSERT INTO Orders (Reference, CustomerId, Status, PickupLat, PickupLng, PickupAddress, DropoffLat, DropoffLng, DropoffAddress, Price, DistanceKm, DeliveryOtp) VALUES (@Reference, @CustomerId, 'PENDING', @PickupLat, @PickupLng, @PickupAddress, @DropoffLat, @DropoffLng, @DropoffAddress, @Price, @DistanceKm, @DeliveryOtp); SELECT CAST(SCOPE_IDENTITY() as int)";
        return await con.ExecuteScalarAsync<int>(sql, o);
    }
    public async Task<Order> GetOrder(int id) {
        using var con = new SqlConnection(_conn);
        return (await con.QueryFirstOrDefaultAsync<Order>("SELECT * FROM Orders WHERE Id=@id", new { id }))!;
    }
    public async Task<bool> AcceptOrder(int orderId, int riderId) {
        using var con = new SqlConnection(_conn);
        var rows = await con.ExecuteAsync("UPDATE Orders SET RiderId=@riderId, Status='ACCEPTED', AcceptedAt=GETDATE() WHERE Id=@orderId AND Status='PENDING'", new { orderId, riderId });
        return rows > 0;
    }
    private double Calc(double lat1, double lon1, double lat2, double lon2) {
        var R = 6371; var dLat = (lat2-lat1)*Math.PI/180; var dLon = (lon2-lon1)*Math.PI/180;
        var a = Math.Sin(dLat/2)*Math.Sin(dLat/2) + Math.Cos(lat1*Math.PI/180)*Math.Cos(lat2*Math.PI/180)*Math.Sin(dLon/2)*Math.Sin(dLon/2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
    }
}
