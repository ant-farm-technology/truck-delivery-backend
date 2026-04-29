namespace TruckDelivery.Tracking.Application.Interfaces;

public interface IDriverGpsCache
{
    Task SetAsync(Guid driverId, double latitude, double longitude, CancellationToken ct = default);
}
