using TruckDelivery.Shared.Common.Domain;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Shipment.Domain.ValueObjects;

public sealed record RouteInfo(double DistanceMeters, double DurationSeconds, string? EncodedPolyline = null)
{
    public static Result<RouteInfo> Create(double distanceMeters, double durationSeconds, string? encodedPolyline = null)
    {
        if (distanceMeters < 0)
            return Result.Failure<RouteInfo>(Error.Validation("RouteInfo.Distance", "Distance cannot be negative."));
        if (durationSeconds < 0)
            return Result.Failure<RouteInfo>(Error.Validation("RouteInfo.Duration", "Duration cannot be negative."));
        return Result.Success(new RouteInfo(distanceMeters, durationSeconds, encodedPolyline));
    }
}
