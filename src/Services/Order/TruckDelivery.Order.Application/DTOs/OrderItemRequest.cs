namespace TruckDelivery.Order.Application.DTOs;

public sealed record OrderItemRequest(
    string ProductName,
    int Quantity,
    decimal WeightKg,
    decimal VolumeCbm,
    string? Notes = null);
