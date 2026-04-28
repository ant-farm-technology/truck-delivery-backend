using MongoDB.Driver;

namespace TruckDelivery.Shipment.Infrastructure.Persistence.Mongo;

public sealed class SagaRepository(IMongoDatabase database) : ISagaRepository
{
    private readonly IMongoCollection<ShipmentSagaState> _collection =
        database.GetCollection<ShipmentSagaState>("shipment_saga_states");

    public async Task<ShipmentSagaState?> GetByShipmentIdAsync(Guid shipmentId, CancellationToken ct = default)
    {
        var filter = Builders<ShipmentSagaState>.Filter.Eq(s => s.ShipmentId, shipmentId);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task UpsertAsync(ShipmentSagaState state, CancellationToken ct = default)
    {
        var filter = Builders<ShipmentSagaState>.Filter.Eq(s => s.SagaId, state.SagaId);
        var options = new ReplaceOptions { IsUpsert = true };
        await _collection.ReplaceOneAsync(filter, state, options, ct);
    }
}
