using Microsoft.EntityFrameworkCore;
using TruckDelivery.Order.Domain.Repositories;
using TruckDelivery.Order.Infrastructure.Persistence;

namespace TruckDelivery.Order.Infrastructure.Repositories;

public sealed class OrderRepository(OrderDbContext dbContext) : IOrderRepository
{
    public async Task<Domain.Aggregates.Order?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyList<Domain.Aggregates.Order>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
    {
        var orders = await dbContext.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);
        return orders;
    }

    public async Task AddAsync(Domain.Aggregates.Order order, CancellationToken ct = default) =>
        await dbContext.Orders.AddAsync(order, ct);

    public void Update(Domain.Aggregates.Order order) =>
        dbContext.Orders.Update(order);
}
