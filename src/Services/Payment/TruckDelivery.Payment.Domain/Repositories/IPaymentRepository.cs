namespace TruckDelivery.Payment.Domain.Repositories;

public interface IPaymentRepository
{
    Task<Aggregates.Payment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Aggregates.Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task AddAsync(Aggregates.Payment payment, CancellationToken ct = default);
    Task UpdateAsync(Aggregates.Payment payment, CancellationToken ct = default);
}
