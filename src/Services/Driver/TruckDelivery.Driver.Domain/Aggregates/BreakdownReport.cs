using TruckDelivery.Driver.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Driver.Domain.Aggregates;

public sealed class BreakdownReport : Entity<Guid>
{
    private BreakdownReport() { }

    private BreakdownReport(Guid id) : base(id) { }

    public Guid DriverId { get; private set; }
    public Guid? VehicleId { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public string PhotoUrlsJson { get; private set; } = "[]";
    public FraudRiskLevel FraudRiskLevel { get; private set; }
    public string? ReviewNote { get; private set; }
    public DateTime ReportedAt { get; private set; }

    public static BreakdownReport Create(
        Guid driverId,
        Guid? vehicleId,
        double latitude,
        double longitude,
        IReadOnlyList<string> photoUrls,
        FraudRiskLevel riskLevel)
    {
        return new BreakdownReport(Guid.NewGuid())
        {
            DriverId = driverId,
            VehicleId = vehicleId,
            Latitude = latitude,
            Longitude = longitude,
            PhotoUrlsJson = System.Text.Json.JsonSerializer.Serialize(photoUrls),
            FraudRiskLevel = riskLevel,
            ReportedAt = DateTime.UtcNow
        };
    }
}
