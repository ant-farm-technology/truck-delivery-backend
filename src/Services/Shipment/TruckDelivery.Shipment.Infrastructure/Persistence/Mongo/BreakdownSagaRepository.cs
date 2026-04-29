using MongoDB.Driver;

namespace TruckDelivery.Shipment.Infrastructure.Persistence.Mongo;

public sealed class BreakdownSagaRepository(IMongoDatabase database) : IBreakdownSagaRepository
{
    private readonly IMongoCollection<BreakdownSagaState> _collection =
        database.GetCollection<BreakdownSagaState>("breakdown_saga_states");

    public async Task<BreakdownSagaState?> GetByShipmentIdAsync(Guid shipmentId, CancellationToken ct = default)
    {
        var filter = Builders<BreakdownSagaState>.Filter.Eq(s => s.ShipmentId, shipmentId);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task UpsertAsync(BreakdownSagaState state, CancellationToken ct = default)
    {
        var filter = Builders<BreakdownSagaState>.Filter.Eq(s => s.SagaId, state.SagaId);
        var options = new ReplaceOptions { IsUpsert = true };
        await _collection.ReplaceOneAsync(filter, state, options, ct);
    }
}
