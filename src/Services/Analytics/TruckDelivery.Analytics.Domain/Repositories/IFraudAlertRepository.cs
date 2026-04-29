using TruckDelivery.Analytics.Domain.Documents;

namespace TruckDelivery.Analytics.Domain.Repositories;

public interface IFraudAlertRepository
{
    Task AddAsync(FraudAlert alert, CancellationToken ct = default);
    Task<IReadOnlyList<FraudAlert>> GetRecentAsync(DateTime from, int limit, CancellationToken ct = default);
    Task<long> CountAsync(DateTime from, CancellationToken ct = default);
}
