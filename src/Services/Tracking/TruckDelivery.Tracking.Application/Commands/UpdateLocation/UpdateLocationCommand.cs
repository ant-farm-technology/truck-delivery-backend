using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Tracking.Application.Commands.UpdateLocation;

public sealed record UpdateLocationCommand(
    Guid DriverId,
    double Latitude,
    double Longitude,
    double? SpeedKmh = null,
    double? HeadingDeg = null) : IRequest<Result>;
