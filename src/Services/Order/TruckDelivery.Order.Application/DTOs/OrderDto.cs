namespace TruckDelivery.Order.Application.DTOs;

public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    string PickupStreet,
    string PickupCity,
    string PickupProvince,
    string DeliveryStreet,
    string DeliveryCity,
    string DeliveryProvince,
    decimal TotalWeightKg,
    decimal TotalVolumeCbm,
    string? Notes,
    string? CancellationReason,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<OrderItemDto> Items,
    Guid? ShipmentId = null);

public sealed record OrderItemDto(
    Guid Id,
    string ProductName,
    int Quantity,
    decimal WeightKg,
    decimal VolumeCbm,
    string? Notes);

public sealed record OrderSummaryDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    string PickupCity,
    string DeliveryCity,
    decimal TotalWeightKg,
    DateTime CreatedAt,
    Guid? ShipmentId = null);
