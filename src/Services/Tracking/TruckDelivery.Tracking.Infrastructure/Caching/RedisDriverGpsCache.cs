using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TruckDelivery.Tracking.Application.Interfaces;

namespace TruckDelivery.Tracking.Infrastructure.Caching;

public sealed class RedisDriverGpsCache(
    IConnectionMultiplexer redis,
    ILogger<RedisDriverGpsCache> logger) : IDriverGpsCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    public async Task SetAsync(Guid driverId, double latitude, double longitude, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var payload = JsonSerializer.Serialize(new
            {
                Lat = latitude,
                Lng = longitude,
                Ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            await db.StringSetAsync($"driver:gps:{driverId}", payload, Ttl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to cache GPS for Driver={DriverId} — fraud gate will use default risk", driverId);
        }
    }
}
