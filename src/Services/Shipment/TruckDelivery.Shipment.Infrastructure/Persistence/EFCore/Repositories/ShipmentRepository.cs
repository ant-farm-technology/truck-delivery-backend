using Microsoft.EntityFrameworkCore;
using TruckDelivery.Shipment.Domain.Repositories;

namespace TruckDelivery.Shipment.Infrastructure.Persistence.EFCore.Repositories;

public sealed class ShipmentRepository(ShipmentDbContext context) : IShipmentRepository
{
    public async Task<Domain.Aggregates.Shipment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Shipments.FindAsync([id], ct);

    public async Task<Domain.Aggregates.Shipment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => await context.Shipments.FirstOrDefaultAsync(s => s.OrderId == orderId, ct);

    public async Task AddAsync(Domain.Aggregates.Shipment shipment, CancellationToken ct = default)
        => await context.Shipments.AddAsync(shipment, ct);

    public Task UpdateAsync(Domain.Aggregates.Shipment shipment, CancellationToken ct = default)
    {
        context.Shipments.Update(shipment);
        return Task.CompletedTask;
    }
}
