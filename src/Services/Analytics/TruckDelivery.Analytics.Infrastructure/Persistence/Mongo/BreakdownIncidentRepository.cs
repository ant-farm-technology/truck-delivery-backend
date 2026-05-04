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
        var filter = Builders<BreakdownIncident>.Filter.And(
            Builders<BreakdownIncident>.Filter.Gte(i => i.ReportedAt, from),
            Builders<BreakdownIncident>.Filter.Eq(i => i.IsSuccessfullyReassigned, true));

        var incidents = await _collection.Find(filter).ToListAsync(ct);
        var times = incidents.Where(i => i.RecoveryTimeMinutes.HasValue)
                             .Select(i => (double)i.RecoveryTimeMinutes!.Value)
                             .ToList();

        return times.Count == 0 ? null : times.Average();
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

}
