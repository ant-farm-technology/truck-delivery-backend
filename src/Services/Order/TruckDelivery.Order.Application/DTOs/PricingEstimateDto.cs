namespace TruckDelivery.Order.Application.DTOs;

public sealed record PricingEstimateDto(
    string VehicleType,
    double DistanceKm,
    decimal BaseFee,
    decimal DistanceFee,
    decimal WeightSurcharge,
    decimal TotalFee,
    string Currency = "VND");
