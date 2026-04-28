using MongoDB.Driver;
using TruckDelivery.Tracking.Domain.Aggregates;
using TruckDelivery.Tracking.Domain.Repositories;

namespace TruckDelivery.Tracking.Infrastructure.Persistence.Mongo;

public sealed class TrackingSessionRepository(IMongoDatabase database) : ITrackingSessionRepository
{
    private readonly IMongoCollection<TrackingSession> _collection =
        database.GetCollection<TrackingSession>("tracking_sessions");

    public async Task<TrackingSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<TrackingSession>.Filter.Eq(s => s.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<TrackingSession?> GetActiveByDriverIdAsync(Guid driverId, CancellationToken ct = default)
    {
        var filter = Builders<TrackingSession>.Filter.And(
            Builders<TrackingSession>.Filter.Eq(s => s.DriverId, driverId),
            Builders<TrackingSession>.Filter.Eq(s => s.IsActive, true));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<TrackingSession?> GetActiveByShipmentIdAsync(Guid shipmentId, CancellationToken ct = default)
    {
        var filter = Builders<TrackingSession>.Filter.And(
            Builders<TrackingSession>.Filter.Eq(s => s.ShipmentId, shipmentId),
            Builders<TrackingSession>.Filter.Eq(s => s.IsActive, true));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(TrackingSession session, CancellationToken ct = default)
        => await _collection.InsertOneAsync(session, cancellationToken: ct);

    public async Task UpdateAsync(TrackingSession session, CancellationToken ct = default)
    {
        var filter = Builders<TrackingSession>.Filter.Eq(s => s.Id, session.Id);
        await _collection.ReplaceOneAsync(filter, session, cancellationToken: ct);
    }
}
