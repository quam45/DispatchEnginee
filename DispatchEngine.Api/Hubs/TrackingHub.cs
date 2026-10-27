using Microsoft.AspNetCore.SignalR;
namespace DispatchEngine.Api.Hubs;

// Server-to-client only. Events are pushed from controllers via IHubContext<TrackingHub>
// after the database has accepted the change. The hub used to expose UpdateRiderLocation and
// OrderStatusChanged as client-callable methods, which let any connected client broadcast a fake
// rider position or order status to everyone, bypassing auth, validation and the database.
public class TrackingHub : Hub {
}
