using MongoDB.Driver;
using TruckDelivery.Analytics.Domain.Documents;
using TruckDelivery.Analytics.Domain.Repositories;

namespace TruckDelivery.Analytics.Infrastructure.Persistence.Mongo;

public sealed class BreakdownIncidentRepository(IMongoDatabase database) : IBreakdownIncidentRepository
{
    private readonly IMongoCollection<BreakdownIncident> _collection =
        database.GetCollection<BreakdownIncident>("breakdown_incidents");

    public async Task AddAsync(BreakdownIncident incident, CancellationToken ct = default)
        => await _collection.InsertOneAsync(incident, cancellationToken: ct);

    public async Task UpdateAsync(BreakdownIncident incident, CancellationToken ct = default)
        => await _collection.ReplaceOneAsync(
            Builders<BreakdownIncident>.Filter.Eq(i => i.Id, incident.Id),
            incident, cancellationToken: ct);

    public async Task<BreakdownIncident?> GetLatestUnresolvedByDriverIdAsync(Guid driverId, CancellationToken ct = default)
        => await _collection
            .Find(Builders<BreakdownIncident>.Filter.And(
                Builders<BreakdownIncident>.Filter.Eq(i => i.DriverId, driverId),
                Builders<BreakdownIncident>.Filter.Eq(i => i.IsResolved, false)))
            .SortByDescending(i => i.ReportedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<BreakdownIncident>> GetRecentAsync(DateTime from, int limit, CancellationToken ct = default)
        => await _collection
            .Find(Builders<BreakdownIncident>.Filter.Gte(i => i.ReportedAt, from))
            .SortByDescending(i => i.ReportedAt)
            .Limit(limit)
            .ToListAsync(ct);

    public async Task<long> CountAsync(DateTime from, CancellationToken ct = default)
        => await _collection.CountDocumentsAsync(
            Builders<BreakdownIncident>.Filter.Gte(i => i.ReportedAt, from), cancellationToken: ct);

    public async Task<long> CountSuccessfullyReassignedAsync(DateTime from, CancellationToken ct = default)
        => await _collection.CountDocumentsAsync(
            Builders<BreakdownIncident>.Filter.And(
                Builders<BreakdownIncident>.Filter.Gte(i => i.ReportedAt, from),
                Builders<BreakdownIncident>.Filter.Eq(i => i.IsSuccessfullyReassigned, true)),
            cancellationToken: ct);

    public async Task<double?> AverageRecoveryTimeMinutesAsync(DateTime from, CancellationToken ct = default)
    {
        var pipeline = new[]
        {
            new BsonDocumentPipelineStageDefinition<BreakdownIncident, AverageResult>(
                new MongoDB.Bson.BsonDocument("$match", new MongoDB.Bson.BsonDocument
                {
                    { "ReportedAt", new MongoDB.Bson.BsonDocument("$gte", from) },
                    { "IsSuccessfullyReassigned", true },
                    { "RecoveryTimeMinutes", new MongoDB.Bson.BsonDocument("$ne", MongoDB.Bson.BsonNull.Value) }
                })),
            new BsonDocumentPipelineStageDefinition<BreakdownIncident, AverageResult>(
                new MongoDB.Bson.BsonDocument("$group", new MongoDB.Bson.BsonDocument
                {
                    { "_id", MongoDB.Bson.BsonNull.Value },
                    { "avg", new MongoDB.Bson.BsonDocument("$avg", "$RecoveryTimeMinutes") }
                }))
        };

        var result = await _collection.Aggregate(PipelineDefinition<BreakdownIncident, AverageResult>.Create(pipeline))
            .FirstOrDefaultAsync(ct);

        return result?.Avg;
    }

    public async Task<IReadOnlyList<(string RiskLevel, long Count)>> CountByRiskLevelAsync(DateTime from, CancellationToken ct = default)
    {
        var filter = Builders<BreakdownIncident>.Filter.Gte(i => i.ReportedAt, from);
        var incidents = await _collection.Find(filter).ToListAsync(ct);
        return incidents
            .GroupBy(i => i.FraudRiskLevel)
            .Select(g => (g.Key, (long)g.Count()))
            .ToList();
    }

    private sealed class AverageResult
    {
        [MongoDB.Bson.Serialization.Attributes.BsonElement("avg")]
        public double? Avg { get; set; }
    }
}
