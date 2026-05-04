using TruckDelivery.Analytics.Domain.Documents;

namespace TruckDelivery.Analytics.Domain.Repositories;

public interface IBreakdownIncidentRepository
{
    Task AddAsync(BreakdownIncident incident, CancellationToken ct = default);
    Task UpdateAsync(BreakdownIncident incident, CancellationToken ct = default);
    Task<BreakdownIncident?> GetLatestUnresolvedByDriverIdAsync(Guid driverId, CancellationToken ct = default);
    Task<IReadOnlyList<BreakdownIncident>> GetRecentAsync(DateTime from, int limit, CancellationToken ct = default);
    Task<long> CountAsync(DateTime from, CancellationToken ct = default);
    Task<long> CountSuccessfullyReassignedAsync(DateTime from, CancellationToken ct = default);
    Task<double?> AverageRecoveryTimeMinutesAsync(DateTime from, CancellationToken ct = default);
    Task<IReadOnlyList<(string RiskLevel, long Count)>> CountByRiskLevelAsync(DateTime from, CancellationToken ct = default);
}
