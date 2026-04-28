using Microsoft.EntityFrameworkCore;
using TruckDelivery.Order.Domain.Aggregates;
using TruckDelivery.Order.Domain.Entities;

namespace TruckDelivery.Order.Infrastructure.Persistence;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Domain.Aggregates.Order> Orders => Set<Domain.Aggregates.Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("order");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderDbContext).Assembly);
    }
}
