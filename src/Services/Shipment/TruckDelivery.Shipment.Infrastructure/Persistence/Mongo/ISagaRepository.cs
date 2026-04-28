namespace TruckDelivery.Shipment.Infrastructure.Persistence.Mongo;

public interface ISagaRepository
{
    Task<ShipmentSagaState?> GetByShipmentIdAsync(Guid shipmentId, CancellationToken ct = default);
    Task UpsertAsync(ShipmentSagaState state, CancellationToken ct = default);
}
