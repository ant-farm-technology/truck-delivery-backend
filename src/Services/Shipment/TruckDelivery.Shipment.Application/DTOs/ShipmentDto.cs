using System.Diagnostics.CodeAnalysis;

namespace TruckDelivery.Shipment.Application.DTOs;

[ExcludeFromCodeCoverage]
public sealed record ShipmentDto(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    string Status,
    string PickupCity,
    string PickupProvince,
    string DeliveryCity,
    string DeliveryProvince,
    decimal TotalWeightKg,
    decimal TotalVolumeCbm,
    Guid? AssignedDriverId,
    Guid? AssignedVehicleId,
    double? DistanceMeters,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime UpdatedAt);
