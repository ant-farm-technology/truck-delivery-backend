using TruckDelivery.Shared.Common.Persistence;

namespace TruckDelivery.Identity.Infrastructure.Persistence;

public sealed class UnitOfWork(IdentityDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => dbContext.SaveChangesAsync(ct);
}
