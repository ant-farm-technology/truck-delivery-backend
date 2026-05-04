using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TruckDelivery.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseMySql(
                "Server=localhost;Port=3306;Database=truck_identity;User=root;Password=root;",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;

        return new IdentityDbContext(options);
    }
}
