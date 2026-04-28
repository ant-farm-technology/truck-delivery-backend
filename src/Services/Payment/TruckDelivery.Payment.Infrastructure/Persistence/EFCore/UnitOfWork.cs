using TruckDelivery.Shared.Common.Persistence;

namespace TruckDelivery.Payment.Infrastructure.Persistence.EFCore;

public sealed class UnitOfWork(PaymentDbContext context) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
