using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.SignalR;
using DispatchEngine.Api.Hubs;
namespace DispatchEngine.Api.Services;
public class OrderBackgroundService {
    private readonly IConfiguration _config;
    private readonly IHubContext<TrackingHub> _hub;
    public OrderBackgroundService(IConfiguration config, IHubContext<TrackingHub> hub) { _config = config; _hub = hub; }

    // Run by Hangfire every minute (see Program.cs): orders still PENDING after 5 minutes
    public async Task ReassignStaleOrders() {
        using var con = new SqlConnection(_config.GetConnectionString("Default"));
        var stale = await con.QueryAsync("SELECT * FROM Orders WHERE Status='PENDING' AND CreatedAt < DATEADD(minute, -5, GETDATE())");
        foreach(var o in stale) {
            // push to nearest riders again, or notify admin
            await _hub.Clients.All.SendAsync("OrderStale", (string)o.Reference);
        }
    }
}
