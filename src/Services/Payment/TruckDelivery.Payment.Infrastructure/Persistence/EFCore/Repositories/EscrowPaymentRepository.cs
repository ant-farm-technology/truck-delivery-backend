using Microsoft.EntityFrameworkCore;
using TruckDelivery.Payment.Domain.Aggregates;
using TruckDelivery.Payment.Domain.Repositories;

namespace TruckDelivery.Payment.Infrastructure.Persistence.EFCore.Repositories;

public sealed class EscrowPaymentRepository(PaymentDbContext context) : IEscrowPaymentRepository
{
    public async Task<EscrowPayment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.EscrowPayments.FindAsync([id], ct);

    public async Task<EscrowPayment?> GetByShipmentIdAsync(Guid shipmentId, CancellationToken ct = default)
        => await context.EscrowPayments.FirstOrDefaultAsync(e => e.ShipmentId == shipmentId, ct);

    public async Task AddAsync(EscrowPayment escrow, CancellationToken ct = default)
        => await context.EscrowPayments.AddAsync(escrow, ct);

    public Task UpdateAsync(EscrowPayment escrow, CancellationToken ct = default)
    {
        context.EscrowPayments.Update(escrow);
        return Task.CompletedTask;
    }
}
