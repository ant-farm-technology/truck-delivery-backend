using MediatR;

namespace TruckDelivery.Analytics.Application.Commands.RecordBreakdown;

public sealed record RecordBreakdownCommand(
    Guid DriverId,
    Guid? VehicleId,
    string FraudRiskLevel,
    double Latitude,
    double Longitude) : IRequest;
