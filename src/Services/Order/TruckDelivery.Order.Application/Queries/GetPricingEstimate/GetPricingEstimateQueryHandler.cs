using MediatR;
using Microsoft.Extensions.Configuration;
using TruckDelivery.Order.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Order.Application.Queries.GetPricingEstimate;

public sealed class GetPricingEstimateQueryHandler(IConfiguration config) : IRequestHandler<GetPricingEstimateQuery, Result<PricingEstimateDto>>
{
    public Task<Result<PricingEstimateDto>> Handle(GetPricingEstimateQuery query, CancellationToken ct)
    {
        var section = config.GetSection($"Pricing:{query.VehicleType}");
        if (!section.Exists())
            return Task.FromResult(Result.Failure<PricingEstimateDto>(
                Error.Validation("Pricing.VehicleType", $"Unknown vehicle type '{query.VehicleType}'")));

        var baseFee = section.GetValue<decimal>("BaseFee");
        var ratePerKm = section.GetValue<decimal>("RatePerKm");
        var thresholdKg = section.GetValue<decimal>("ThresholdKg");
        var surchargePerKg = section.GetValue<decimal>("SurchargePerKg");

        var distanceKm = Haversine(query.PickupLat, query.PickupLng, query.DeliveryLat, query.DeliveryLng);
        var distanceFee = Math.Round(ratePerKm * (decimal)distanceKm, 0);
        var weightSurcharge = Math.Max(0, query.WeightKg - thresholdKg) * surchargePerKg;
        var totalFee = baseFee + distanceFee + weightSurcharge;

        var dto = new PricingEstimateDto(
            query.VehicleType,
            Math.Round(distanceKm, 1),
            baseFee,
            distanceFee,
            weightSurcharge,
            totalFee);

        return Task.FromResult(Result.Success(dto));
    }

    private static double Haversine(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLng = (lng2 - lng1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
