namespace TruckDelivery.Shipment.Infrastructure.Persistence.Mongo;

public interface IBreakdownSagaRepository
{
    Task<BreakdownSagaState?> GetByShipmentIdAsync(Guid shipmentId, CancellationToken ct = default);
    Task UpsertAsync(BreakdownSagaState state, CancellationToken ct = default);
}
