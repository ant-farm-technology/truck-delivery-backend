using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Order.Application.IntegrationEvents;

public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    string PickupCity,
    string PickupProvince,
    string DeliveryCity,
    string DeliveryProvince,
    decimal TotalWeightKg,
    decimal TotalVolumeCbm,
    IReadOnlyList<OrderItemInfo> Items) : IntegrationEvent;

public sealed record OrderItemInfo(
    Guid ItemId,
    string ProductName,
    int Quantity,
    decimal WeightKg,
    decimal VolumeCbm,
    decimal? LengthM,
    decimal? WidthM,
    decimal? HeightM,
    bool CanTilt);
