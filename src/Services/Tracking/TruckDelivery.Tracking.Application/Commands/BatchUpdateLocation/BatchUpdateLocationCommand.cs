using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Tracking.Application.Commands.BatchUpdateLocation;

public sealed record LocationPointDto(
    double Latitude,
    double Longitude,
    double? SpeedKmh,
    double? HeadingDeg,
    DateTime RecordedAt);

public sealed record BatchUpdateLocationCommand(
    Guid DriverId,
    IReadOnlyList<LocationPointDto> Points) : IRequest<Result>;
