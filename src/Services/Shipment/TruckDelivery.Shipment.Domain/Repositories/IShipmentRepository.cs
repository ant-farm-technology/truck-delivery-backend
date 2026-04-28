namespace TruckDelivery.Shipment.Domain.Repositories;

public interface IShipmentRepository
{
    Task<Aggregates.Shipment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Aggregates.Shipment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task AddAsync(Aggregates.Shipment shipment, CancellationToken ct = default);
    Task UpdateAsync(Aggregates.Shipment shipment, CancellationToken ct = default);
}
