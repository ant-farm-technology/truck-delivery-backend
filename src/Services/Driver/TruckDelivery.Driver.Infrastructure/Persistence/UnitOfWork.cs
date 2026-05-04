using TruckDelivery.Shared.Common.Persistence;

namespace TruckDelivery.Driver.Infrastructure.Persistence;

public sealed class UnitOfWork(DriverDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        dbContext.SaveChangesAsync(ct);
}
