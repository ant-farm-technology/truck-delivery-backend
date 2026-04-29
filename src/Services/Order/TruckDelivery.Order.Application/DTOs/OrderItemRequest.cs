namespace TruckDelivery.Order.Application.DTOs;

public sealed record OrderItemRequest(
    string ProductName,
    int Quantity,
    decimal WeightKg,
    decimal VolumeCbm,
    decimal? LengthM = null,
    decimal? WidthM = null,
    decimal? HeightM = null,
    bool CanTilt = false,
    string? Notes = null);
