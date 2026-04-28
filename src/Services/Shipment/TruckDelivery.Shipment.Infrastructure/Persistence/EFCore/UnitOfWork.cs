using TruckDelivery.Shared.Common.Persistence;

namespace TruckDelivery.Shipment.Infrastructure.Persistence.EFCore;

public sealed class UnitOfWork(ShipmentDbContext context) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken);
}
