using Microsoft.EntityFrameworkCore;
using TruckDelivery.Payment.Domain.Aggregates;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Payment.Infrastructure.Persistence.EFCore;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<Domain.Aggregates.Payment> Payments => Set<Domain.Aggregates.Payment>();
    public DbSet<EscrowPayment> EscrowPayments => Set<EscrowPayment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);
    }
}
