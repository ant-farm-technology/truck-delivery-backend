using TruckDelivery.Shared.Common.Persistence;

namespace TruckDelivery.Order.Infrastructure.Persistence;

public sealed class UnitOfWork(OrderDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        dbContext.SaveChangesAsync(ct);
}
