using TruckDelivery.Shared.Common.Persistence;

namespace TruckDelivery.Notification.Infrastructure.Persistence.EFCore;

public sealed class UnitOfWork(NotificationDbContext context) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
