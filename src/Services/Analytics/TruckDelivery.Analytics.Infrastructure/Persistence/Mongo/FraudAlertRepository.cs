using MongoDB.Driver;
using TruckDelivery.Analytics.Domain.Documents;
using TruckDelivery.Analytics.Domain.Repositories;

namespace TruckDelivery.Analytics.Infrastructure.Persistence.Mongo;

public sealed class FraudAlertRepository(IMongoDatabase database) : IFraudAlertRepository
{
    private readonly IMongoCollection<FraudAlert> _collection =
        database.GetCollection<FraudAlert>("fraud_alerts");

    public async Task AddAsync(FraudAlert alert, CancellationToken ct = default)
        => await _collection.InsertOneAsync(alert, cancellationToken: ct);

    public async Task<IReadOnlyList<FraudAlert>> GetRecentAsync(DateTime from, int limit, CancellationToken ct = default)
        => await _collection
            .Find(Builders<FraudAlert>.Filter.Gte(a => a.DetectedAt, from))
            .SortByDescending(a => a.DetectedAt)
            .Limit(limit)
            .ToListAsync(ct);

    public async Task<long> CountAsync(DateTime from, CancellationToken ct = default)
        => await _collection.CountDocumentsAsync(
            Builders<FraudAlert>.Filter.Gte(a => a.DetectedAt, from), cancellationToken: ct);
}
