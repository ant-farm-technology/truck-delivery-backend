using TruckDelivery.Driver.Domain.ValueObjects;

namespace TruckDelivery.Driver.Domain.Repositories;

public interface IVehicleRepository
{
    Task<Aggregates.Vehicle?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Aggregates.Vehicle?> GetByLicensePlateAsync(string licensePlate, CancellationToken ct = default);
    Task<bool> ExistsByLicensePlateAsync(string licensePlate, CancellationToken ct = default);
    Task<IReadOnlyList<Aggregates.Vehicle>> GetAvailableVehiclesAsync(CancellationToken ct = default);
    Task AddAsync(Aggregates.Vehicle vehicle, CancellationToken ct = default);
    void Update(Aggregates.Vehicle vehicle);
}
