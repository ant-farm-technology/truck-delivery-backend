using Microsoft.EntityFrameworkCore;
using TruckDelivery.Driver.Domain.Aggregates;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Driver.Domain.ValueObjects;
using TruckDelivery.Driver.Infrastructure.Persistence;

namespace TruckDelivery.Driver.Infrastructure.Repositories;

public sealed class VehicleRepository(DriverDbContext dbContext) : IVehicleRepository
{
    public async Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<Vehicle?> GetByLicensePlateAsync(string licensePlate, CancellationToken ct = default) =>
        await dbContext.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == licensePlate.ToUpperInvariant(), ct);

    public async Task<bool> ExistsByLicensePlateAsync(string licensePlate, CancellationToken ct = default) =>
        await dbContext.Vehicles.AnyAsync(v => v.LicensePlate == licensePlate.ToUpperInvariant(), ct);

    public async Task<IReadOnlyList<Vehicle>> GetAvailableVehiclesAsync(CancellationToken ct = default)
    {
        var vehicles = await dbContext.Vehicles
            .Where(v => v.Status == VehicleStatus.Available)
            .ToListAsync(ct);
        return vehicles;
    }

    public async Task AddAsync(Vehicle vehicle, CancellationToken ct = default) =>
        await dbContext.Vehicles.AddAsync(vehicle, ct);

    public void Update(Vehicle vehicle) =>
        dbContext.Vehicles.Update(vehicle);
}
