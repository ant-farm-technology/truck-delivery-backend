using TruckDelivery.Driver.Domain.Aggregates;

namespace TruckDelivery.Driver.Domain.Repositories;

public interface IDriverSwapRecordRepository
{
    Task AddAsync(DriverSwapRecord record, CancellationToken ct = default);

    // Returns count of times replacementDriverId replaced originalDriverId
    Task<int> CountSwapPairsAsync(Guid originalDriverId, Guid replacementDriverId, CancellationToken ct = default);

    // Returns all swap records involving a driver as original or replacement
    Task<IReadOnlyList<DriverSwapRecord>> GetByDriverIdAsync(Guid driverId, CancellationToken ct = default);
}
