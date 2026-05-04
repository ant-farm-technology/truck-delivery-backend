using Microsoft.EntityFrameworkCore;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Driver.Domain.ValueObjects;
using TruckDelivery.Driver.Infrastructure.Persistence;

namespace TruckDelivery.Driver.Infrastructure.Repositories;

public sealed class DriverRepository(DriverDbContext dbContext) : IDriverRepository
{
    public async Task<Domain.Aggregates.Driver?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Drivers.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<Domain.Aggregates.Driver?> GetByLicenseNumberAsync(string licenseNumber, CancellationToken ct = default) =>
        await dbContext.Drivers.FirstOrDefaultAsync(d => d.LicenseNumber == licenseNumber.ToUpperInvariant(), ct);

    public async Task<bool> ExistsByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Drivers.AnyAsync(d => d.Id == id, ct);

    public async Task<bool> ExistsByIdCardNumberAsync(string idCardNumber, CancellationToken ct = default) =>
        await dbContext.Drivers.AnyAsync(d => d.IdCardNumber == idCardNumber, ct);

    public async Task<IReadOnlyList<Domain.Aggregates.Driver>> GetAvailableDriversAsync(CancellationToken ct = default)
    {
        var drivers = await dbContext.Drivers
            .Where(d => d.Status == DriverStatus.Available && d.IsActive)
            .ToListAsync(ct);
        return drivers;
    }

    public async Task AddAsync(Domain.Aggregates.Driver driver, CancellationToken ct = default) =>
        await dbContext.Drivers.AddAsync(driver, ct);

    public void Update(Domain.Aggregates.Driver driver) =>
        dbContext.Drivers.Update(driver);
}
