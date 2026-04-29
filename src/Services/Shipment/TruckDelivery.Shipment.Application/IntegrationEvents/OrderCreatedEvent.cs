using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Shipment.Application.IntegrationEvents;

// Consumer-side DTO — mirrors the shape published by Order Service
public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    string PickupCity,
    string PickupProvince,
    string DeliveryCity,
    string DeliveryProvince,
    decimal TotalWeightKg,
    decimal TotalVolumeCbm,
    IReadOnlyList<ShipmentPackageInfo>? Items = null) : IntegrationEvent;

public sealed record ShipmentPackageInfo(
    Guid ItemId,
    string ProductName,
    int Quantity,
    decimal WeightKg,
    decimal VolumeCbm,
    decimal? LengthM,
    decimal? WidthM,
    decimal? HeightM,
    bool CanTilt);
