using MongoDB.Bson.Serialization.Attributes;

namespace TruckDelivery.Analytics.Domain.Documents;

public sealed class BreakdownIncident
{
    [BsonId] public Guid Id { get; set; }
    public Guid DriverId { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? ShipmentId { get; set; }
    public string FraudRiskLevel { get; set; } = "Unknown";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime ReportedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool IsResolved { get; set; }
    public bool IsSuccessfullyReassigned { get; set; }
    public int? RecoveryTimeMinutes { get; set; }

    private BreakdownIncident() { }

    public static BreakdownIncident Create(
        Guid driverId, Guid? vehicleId,
        string fraudRiskLevel, double latitude, double longitude)
        => new()
        {
            Id = Guid.NewGuid(),
            DriverId = driverId,
            VehicleId = vehicleId,
            FraudRiskLevel = fraudRiskLevel,
            Latitude = latitude,
            Longitude = longitude,
            ReportedAt = DateTime.UtcNow,
            IsResolved = false,
            IsSuccessfullyReassigned = false
        };

    public void MarkResolved(Guid shipmentId, bool isReassigned)
    {
        ShipmentId = shipmentId;
        IsResolved = true;
        IsSuccessfullyReassigned = isReassigned;
        ResolvedAt = DateTime.UtcNow;
        if (isReassigned)
            RecoveryTimeMinutes = (int)(ResolvedAt.Value - ReportedAt).TotalMinutes;
    }
}
