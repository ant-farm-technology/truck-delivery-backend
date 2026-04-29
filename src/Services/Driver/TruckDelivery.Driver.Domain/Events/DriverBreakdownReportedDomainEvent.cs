using TruckDelivery.Driver.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Driver.Domain.Events;

public sealed record DriverBreakdownReportedDomainEvent(
    Guid DriverId,
    Guid? VehicleId,
    double Latitude,
    double Longitude,
    IReadOnlyList<string> PhotoUrls,
    int TrustScore,
    FraudRiskLevel FraudRiskLevel) : IDomainEvent;
