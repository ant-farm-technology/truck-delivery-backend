using TruckDelivery.Payment.Domain.Aggregates;

namespace TruckDelivery.Payment.Domain.Repositories;

public interface IEscrowPaymentRepository
{
    Task<EscrowPayment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<EscrowPayment?> GetByShipmentIdAsync(Guid shipmentId, CancellationToken ct = default);
    Task AddAsync(EscrowPayment escrow, CancellationToken ct = default);
    Task UpdateAsync(EscrowPayment escrow, CancellationToken ct = default);
}
