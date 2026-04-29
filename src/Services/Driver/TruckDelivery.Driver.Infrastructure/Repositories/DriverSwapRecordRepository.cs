using Microsoft.EntityFrameworkCore;
using TruckDelivery.Driver.Domain.Aggregates;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Driver.Infrastructure.Persistence;

namespace TruckDelivery.Driver.Infrastructure.Repositories;

public sealed class DriverSwapRecordRepository(DriverDbContext context) : IDriverSwapRecordRepository
{
    public async Task AddAsync(DriverSwapRecord record, CancellationToken ct = default)
        => await context.DriverSwapRecords.AddAsync(record, ct);

    public async Task<int> CountSwapPairsAsync(Guid originalDriverId, Guid replacementDriverId, CancellationToken ct = default)
        => await context.DriverSwapRecords
            .CountAsync(r => r.OriginalDriverId == originalDriverId
                          && r.ReplacementDriverId == replacementDriverId, ct);

    public async Task<IReadOnlyList<DriverSwapRecord>> GetByDriverIdAsync(Guid driverId, CancellationToken ct = default)
        => await context.DriverSwapRecords
            .Where(r => r.OriginalDriverId == driverId || r.ReplacementDriverId == driverId)
            .OrderByDescending(r => r.OccurredAt)
            .ToListAsync(ct);
}
