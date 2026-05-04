using MongoDB.Driver;
using TruckDelivery.Tracking.Domain.Aggregates;
using TruckDelivery.Tracking.Domain.Repositories;

namespace TruckDelivery.Tracking.Infrastructure.Persistence.Mongo;

public sealed class TrackingPointRepository(IMongoDatabase database) : ITrackingPointRepository
{
    private readonly IMongoCollection<TrackingPoint> _collection =
        database.GetCollection<TrackingPoint>("tracking_points");

    public async Task AddAsync(TrackingPoint point, CancellationToken ct = default)
        => await _collection.InsertOneAsync(point, cancellationToken: ct);

    public async Task AddManyAsync(IReadOnlyList<TrackingPoint> points, CancellationToken ct = default)
        => await _collection.InsertManyAsync(points, cancellationToken: ct);

    public async Task<IReadOnlyList<TrackingPoint>> GetByShipmentIdAsync(
        Guid shipmentId, int limit = 100, CancellationToken ct = default)
    {
        var filter = Builders<TrackingPoint>.Filter.Eq(p => p.ShipmentId, shipmentId);
        var sort = Builders<TrackingPoint>.Sort.Descending(p => p.RecordedAt);
        var results = await _collection.Find(filter).Sort(sort).Limit(limit).ToListAsync(ct);
        return results.AsReadOnly();
    }
}
