using System.Data;
using Dapper;
using Microsoft.AspNetCore.SignalR;
using DispatchEngine.Api.Hubs;
using DispatchEngine.Infrastructure;
namespace DispatchEngine.Api.Services;
public class OrderBackgroundService {
    private readonly IDbConnectionFactory _db;
    private readonly IHubContext<TrackingHub> _hub;
    public OrderBackgroundService(IDbConnectionFactory db, IHubContext<TrackingHub> hub) { _db = db; _hub = hub; }

    // Run by Hangfire every minute (see Program.cs): orders still PENDING after 5 minutes
    public async Task ReassignStaleOrders() {
        using var con = _db.CreateConnection();
        var stale = await con.QueryAsync("dbo.sp_GetStaleOrders", new { OlderThanMinutes = 5 }, commandType: CommandType.StoredProcedure);
        foreach(var o in stale) {
            // push to nearest riders again, or notify admin
            await _hub.Clients.All.SendAsync("OrderStale", (string)o.Reference);
        }
    }
}
