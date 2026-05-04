using TruckDelivery.Driver.Domain.ValueObjects;

namespace TruckDelivery.Driver.Domain.Repositories;

public interface IDriverRepository
{
    Task<Aggregates.Driver?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Aggregates.Driver?> GetByLicenseNumberAsync(string licenseNumber, CancellationToken ct = default);
    Task<bool> ExistsByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsByIdCardNumberAsync(string idCardNumber, CancellationToken ct = default);
    Task<IReadOnlyList<Aggregates.Driver>> GetAvailableDriversAsync(CancellationToken ct = default);
    Task AddAsync(Aggregates.Driver driver, CancellationToken ct = default);
    void Update(Aggregates.Driver driver);
}
