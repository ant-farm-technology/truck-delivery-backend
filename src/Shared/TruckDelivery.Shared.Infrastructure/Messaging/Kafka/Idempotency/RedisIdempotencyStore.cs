using StackExchange.Redis;

namespace TruckDelivery.Shared.Infrastructure.Messaging.Kafka.Idempotency;

public sealed class RedisIdempotencyStore(IConnectionMultiplexer redis) : IIdempotencyStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);
    private readonly IDatabase _database = redis.GetDatabase();

    public async Task<bool> HasProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return await _database.KeyExistsAsync(BuildKey(messageId));
    }

    public async Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        await _database.StringSetAsync(BuildKey(messageId), "1", Ttl);
    }

    private static string BuildKey(Guid messageId) => $"idempotency:{messageId}";
}
