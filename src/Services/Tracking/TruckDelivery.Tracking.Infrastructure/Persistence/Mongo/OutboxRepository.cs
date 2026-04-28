using MongoDB.Driver;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Tracking.Infrastructure.Persistence.Mongo;

// MongoDB-backed outbox for the stateless Tracking service (no MySQL)
public sealed class MongoOutboxRepository(IMongoDatabase database) : IOutboxRepository
{
    private readonly IMongoCollection<OutboxMessage> _collection =
        database.GetCollection<OutboxMessage>("outbox_messages");

    public async Task AddAsync(OutboxMessage message, CancellationToken ct = default)
        => await _collection.InsertOneAsync(message, cancellationToken: ct);

    public async Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int batchSize, CancellationToken ct = default)
    {
        var filter = Builders<OutboxMessage>.Filter.And(
            Builders<OutboxMessage>.Filter.Eq(m => m.ProcessedAt, null),
            Builders<OutboxMessage>.Filter.Lt(m => m.RetryCount, 5));
        var sort = Builders<OutboxMessage>.Sort.Ascending(m => m.OccurredAt);
        var results = await _collection.Find(filter).Sort(sort).Limit(batchSize).ToListAsync(ct);
        return results.AsReadOnly();
    }

    public async Task MarkProcessedAsync(Guid messageId, CancellationToken ct = default)
    {
        var filter = Builders<OutboxMessage>.Filter.Eq(m => m.Id, messageId);
        var update = Builders<OutboxMessage>.Update.Set(m => m.ProcessedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public async Task MarkFailedAsync(Guid messageId, string error, CancellationToken ct = default)
    {
        var filter = Builders<OutboxMessage>.Filter.Eq(m => m.Id, messageId);
        var update = Builders<OutboxMessage>.Update
            .Inc(m => m.RetryCount, 1)
            .Set(m => m.LastError, error);
        await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
