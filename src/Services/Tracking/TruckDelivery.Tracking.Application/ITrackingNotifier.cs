namespace TruckDelivery.Tracking.Application;

public interface ITrackingNotifier
{
    Task NotifyLocationUpdatedAsync(
        Guid shipmentId,
        Guid driverId,
        double latitude,
        double longitude,
        double? speedKmh,
        DateTime recordedAt,
        CancellationToken ct = default);

    Task NotifyDriverAssignedAsync(
        Guid driverId,
        Guid shipmentId,
        Guid orderId,
        Guid vehicleId,
        CancellationToken ct = default);
}
