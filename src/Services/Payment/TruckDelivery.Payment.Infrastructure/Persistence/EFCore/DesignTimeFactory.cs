using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TruckDelivery.Payment.Infrastructure.Persistence.EFCore;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PaymentDbContext>();
        optionsBuilder.UseMySql(
            "Server=localhost;Port=3306;Database=truck_payment;User=root;Password=root;",
            ServerVersion.AutoDetect("Server=localhost;Port=3306;Database=truck_payment;User=root;Password=root;"));
        return new PaymentDbContext(optionsBuilder.Options);
    }
}
