using TruckDelivery.Tracking.Domain.Aggregates;

namespace TruckDelivery.Tracking.Domain.Repositories;

public interface ITrackingPointRepository
{
    Task AddAsync(TrackingPoint point, CancellationToken ct = default);
    Task AddManyAsync(IReadOnlyList<TrackingPoint> points, CancellationToken ct = default);
    Task<IReadOnlyList<TrackingPoint>> GetByShipmentIdAsync(Guid shipmentId, int limit = 100, CancellationToken ct = default);
}
