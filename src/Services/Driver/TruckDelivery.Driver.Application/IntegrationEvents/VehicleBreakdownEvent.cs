using TruckDelivery.Driver.Domain.ValueObjects;
using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Driver.Application.IntegrationEvents;

public sealed record VehicleBreakdownEvent : IntegrationEvent
{
    public Guid DriverId { get; init; }
    public Guid? VehicleId { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public IReadOnlyList<string> PhotoUrls { get; init; } = [];
    public int TrustScore { get; init; }
    public FraudRiskLevel FraudRiskLevel { get; init; }
}
