using TruckDelivery.Tracking.Domain.Events;
using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Tracking.Domain.Aggregates;

public sealed class TrackingSession : AggregateRoot<Guid>
{
    private TrackingSession() { }
    private TrackingSession(Guid id) : base(id) { }

    public Guid ShipmentId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid DriverId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public double? LastLatitude { get; private set; }
    public double? LastLongitude { get; private set; }
    public DateTime? LastUpdatedAt { get; private set; }

    public static TrackingSession Start(Guid shipmentId, Guid orderId, Guid driverId)
    {
        var session = new TrackingSession(Guid.NewGuid())
        {
            ShipmentId = shipmentId,
            OrderId = orderId,
            DriverId = driverId,
            IsActive = true,
            StartedAt = DateTime.UtcNow
        };
        session.RaiseDomainEvent(new TrackingSessionStartedDomainEvent(session.Id, shipmentId, driverId));
        return session;
    }

    public void UpdateLocation(double latitude, double longitude)
    {
        LastLatitude = latitude;
        LastLongitude = longitude;
        LastUpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new LocationUpdatedDomainEvent(Id, ShipmentId, DriverId, latitude, longitude));
    }

    public void Stop()
    {
        IsActive = false;
        EndedAt = DateTime.UtcNow;
    }
}
