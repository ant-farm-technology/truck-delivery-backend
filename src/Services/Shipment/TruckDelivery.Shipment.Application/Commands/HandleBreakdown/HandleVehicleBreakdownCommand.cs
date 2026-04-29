using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Shipment.Application.Commands.HandleBreakdown;

public sealed record HandleVehicleBreakdownCommand(
    Guid DriverId,
    Guid? VehicleId,
    double Latitude,
    double Longitude,
    int TrustScore,
    string FraudRiskLevel) : IRequest<Result>;
