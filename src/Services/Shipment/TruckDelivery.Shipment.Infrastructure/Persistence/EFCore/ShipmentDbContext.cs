using Microsoft.EntityFrameworkCore;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Shipment.Infrastructure.Persistence.EFCore;

public sealed class ShipmentDbContext(DbContextOptions<ShipmentDbContext> options) : DbContext(options)
{
    public DbSet<Domain.Aggregates.Shipment> Shipments => Set<Domain.Aggregates.Shipment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("shipment");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShipmentDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration("shipment"));
    }
}
