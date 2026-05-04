namespace TruckDelivery.Tracking.Application.DTOs;

public sealed record TrackingPointDto(
    Guid DriverId,
    double Latitude,
    double Longitude,
    double? SpeedKmh,
    DateTime RecordedAt);
