using Microsoft.AspNetCore.SignalR;
using TruckDelivery.Tracking.Application;

namespace TruckDelivery.Tracking.Infrastructure.Hubs;

public sealed class TrackingHubNotifier(IHubContext<TrackingHub> hubContext) : ITrackingNotifier
{
    public Task NotifyLocationUpdatedAsync(
        Guid shipmentId,
        Guid driverId,
        double latitude,
        double longitude,
        double? speedKmh,
        DateTime recordedAt,
        CancellationToken ct = default)
        => hubContext.Clients
            .Group($"shipment:{shipmentId}")
            .SendAsync("LocationUpdated", new
            {
                driverId,
                latitude,
                longitude,
                speedKmh,
                recordedAt
            }, ct);

    public Task NotifyDriverAssignedAsync(
        Guid driverId,
        Guid shipmentId,
        Guid orderId,
        Guid vehicleId,
        CancellationToken ct = default)
        => hubContext.Clients
            .Group($"driver:{driverId}")
            .SendAsync("DriverAssigned", new
            {
                shipmentId,
                orderId,
                vehicleId,
                assignedAt = DateTime.UtcNow
            }, ct);
}
