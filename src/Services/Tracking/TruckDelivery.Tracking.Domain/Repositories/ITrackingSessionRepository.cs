using TruckDelivery.Tracking.Domain.Aggregates;

namespace TruckDelivery.Tracking.Domain.Repositories;

public interface ITrackingSessionRepository
{
    Task<TrackingSession?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TrackingSession?> GetActiveByDriverIdAsync(Guid driverId, CancellationToken ct = default);
    Task<TrackingSession?> GetActiveByShipmentIdAsync(Guid shipmentId, CancellationToken ct = default);
    Task AddAsync(TrackingSession session, CancellationToken ct = default);
    Task UpdateAsync(TrackingSession session, CancellationToken ct = default);
}
