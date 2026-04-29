using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TruckDelivery.Driver.Application.Interfaces;
using TruckDelivery.Driver.Domain.ValueObjects;

namespace TruckDelivery.Driver.Infrastructure.Services;

/// <summary>
/// Validates breakdown reports using GPS cache, photo presence, and trust score.
/// GPS cache is written by Tracking service under key driver:gps:{driverId}.
/// </summary>
public sealed class BreakdownFraudGate(
    IConnectionMultiplexer redis,
    ILogger<BreakdownFraudGate> logger) : IBreakdownFraudGate
{
    private const double MaxAllowedDistanceKm = 2.0;
    private const int MinTrustScore = 30;

    public async Task<FraudGateResult> ValidateAsync(
        Guid driverId,
        double latitude,
        double longitude,
        IReadOnlyList<string> photoUrls,
        int trustScore,
        CancellationToken ct = default)
    {
        if (trustScore < MinTrustScore)
        {
            logger.LogWarning("Breakdown rejected — TrustScore={Score} below minimum for Driver={DriverId}", trustScore, driverId);
            return new FraudGateResult(false, FraudRiskLevel.High, $"Trust score {trustScore} is too low to report breakdown.");
        }

        if (photoUrls.Count == 0)
        {
            logger.LogWarning("Breakdown rejected — no photos for Driver={DriverId}", driverId);
            return new FraudGateResult(false, FraudRiskLevel.Medium, "At least one breakdown photo is required.");
        }

        var riskLevel = await AssessGpsRiskAsync(driverId, latitude, longitude);

        logger.LogInformation(
            "Breakdown gate passed for Driver={DriverId} RiskLevel={Risk} TrustScore={Score}",
            driverId, riskLevel, trustScore);

        return new FraudGateResult(true, riskLevel, null);
    }

    private async Task<FraudRiskLevel> AssessGpsRiskAsync(Guid driverId, double reportedLat, double reportedLng)
    {
        try
        {
            var db = redis.GetDatabase();
            var cached = await db.StringGetAsync($"driver:gps:{driverId}");
            if (!cached.HasValue)
                return FraudRiskLevel.Low; // no cache = cannot assess, assume low risk

            var gps = JsonSerializer.Deserialize<CachedGps>(cached!);
            if (gps is null)
                return FraudRiskLevel.Low;

            var distanceKm = Haversine(gps.Lat, gps.Lng, reportedLat, reportedLng);

            if (distanceKm > MaxAllowedDistanceKm)
            {
                logger.LogWarning(
                    "GPS mismatch for Driver={DriverId}: cached=({CLat},{CLng}) reported=({RLat},{RLng}) dist={Dist:F2}km",
                    driverId, gps.Lat, gps.Lng, reportedLat, reportedLng, distanceKm);
                return FraudRiskLevel.Medium;
            }

            return FraudRiskLevel.Low;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis GPS check failed for Driver={DriverId} — defaulting to Low risk", driverId);
            return FraudRiskLevel.Low;
        }
    }

    private static double Haversine(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371.0;
        var dLat = ToRad(lat2 - lat1);
        var dLng = ToRad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
              * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;

    private sealed record CachedGps(double Lat, double Lng, long Ts);
}
