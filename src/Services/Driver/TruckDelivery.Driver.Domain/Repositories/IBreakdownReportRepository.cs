using TruckDelivery.Driver.Domain.Aggregates;

namespace TruckDelivery.Driver.Domain.Repositories;

public interface IBreakdownReportRepository
{
    Task AddAsync(BreakdownReport report, CancellationToken ct = default);
    Task<IReadOnlyList<BreakdownReport>> GetByDriverIdAsync(Guid driverId, CancellationToken ct = default);
}
