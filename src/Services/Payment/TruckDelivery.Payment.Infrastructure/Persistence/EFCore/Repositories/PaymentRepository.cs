using Microsoft.EntityFrameworkCore;
using TruckDelivery.Payment.Domain.Repositories;

namespace TruckDelivery.Payment.Infrastructure.Persistence.EFCore.Repositories;

public sealed class PaymentRepository(PaymentDbContext context) : IPaymentRepository
{
    public async Task<Domain.Aggregates.Payment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Payments.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Domain.Aggregates.Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => await context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

    public async Task AddAsync(Domain.Aggregates.Payment payment, CancellationToken ct = default)
        => await context.Payments.AddAsync(payment, ct);

    public Task UpdateAsync(Domain.Aggregates.Payment payment, CancellationToken ct = default)
    {
        context.Payments.Update(payment);
        return Task.CompletedTask;
    }
}
