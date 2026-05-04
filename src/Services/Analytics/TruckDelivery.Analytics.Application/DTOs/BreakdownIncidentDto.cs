namespace TruckDelivery.Analytics.Application.DTOs;

public sealed record BreakdownIncidentDto(
    Guid Id,
    Guid DriverId,
    Guid? VehicleId,
    Guid? ShipmentId,
    string FraudRiskLevel,
    double Latitude,
    double Longitude,
    DateTime ReportedAt,
    DateTime? ResolvedAt,
    bool IsResolved,
    bool IsSuccessfullyReassigned,
    int? RecoveryTimeMinutes);
