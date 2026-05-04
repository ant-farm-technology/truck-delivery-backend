using TruckDelivery.Order.Domain.Aggregates;

namespace TruckDelivery.Order.Domain.Repositories;

public interface IOrderRepository
{
    Task<Aggregates.Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Aggregates.Order>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
    Task AddAsync(Aggregates.Order order, CancellationToken ct = default);
    void Update(Aggregates.Order order);
}
