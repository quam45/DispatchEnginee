using Microsoft.AspNetCore.SignalR;
namespace DispatchEngine.Api.Hubs;
public class TrackingHub : Hub {
    public async Task UpdateRiderLocation(int riderId, double lat, double lng) {
        await Clients.All.SendAsync("RiderLocationUpdated", riderId, lat, lng);
    }
    public async Task OrderStatusChanged(string orderRef, string status) {
        await Clients.All.SendAsync("OrderStatus", orderRef, status);
    }
}
