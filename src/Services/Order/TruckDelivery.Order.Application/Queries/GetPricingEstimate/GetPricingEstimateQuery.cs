using MediatR;
using TruckDelivery.Order.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Order.Application.Queries.GetPricingEstimate;

public sealed record GetPricingEstimateQuery(
    string VehicleType,
    double PickupLat,
    double PickupLng,
    double DeliveryLat,
    double DeliveryLng,
    decimal WeightKg = 0) : IRequest<Result<PricingEstimateDto>>;
