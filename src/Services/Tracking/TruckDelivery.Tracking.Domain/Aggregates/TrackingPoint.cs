namespace TruckDelivery.Tracking.Domain.Aggregates;

// Stored in MongoDB as a time-series document per driver location update
public sealed class TrackingPoint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid ShipmentId { get; set; }
    public Guid DriverId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? SpeedKmh { get; set; }
    public double? HeadingDeg { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
