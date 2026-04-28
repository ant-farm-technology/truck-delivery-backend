using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace TruckDelivery.Tracking.Infrastructure.Hubs;

[Authorize]
public sealed class TrackingHub(ILogger<TrackingHub> logger) : Hub
{
    // Client joins a group to receive updates for a specific shipment
    public async Task JoinShipmentGroup(string shipmentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"shipment:{shipmentId}");
        logger.LogInformation("ConnectionId={ConnectionId} joined shipment:{ShipmentId}", Context.ConnectionId, shipmentId);
    }

    public async Task LeaveShipmentGroup(string shipmentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"shipment:{shipmentId}");
    }

    // Driver joins own group so dispatcher can push messages to specific driver
    public async Task JoinDriverGroup(string driverId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"driver:{driverId}");
        logger.LogInformation("ConnectionId={ConnectionId} joined driver:{DriverId}", Context.ConnectionId, driverId);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
