using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TruckDelivery.Order.Infrastructure.Persistence;

public sealed class OrderDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    public OrderDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseMySql(
                "Server=localhost;Port=3306;Database=truck_order;User=root;Password=root;",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;

        return new OrderDbContext(options);
    }
}
