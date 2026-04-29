using MediatR;
using TruckDelivery.Analytics.Application.DTOs;
using TruckDelivery.Analytics.Domain.Repositories;

namespace TruckDelivery.Analytics.Application.Queries.GetFraudAlerts;

public sealed class GetFraudAlertsQueryHandler(IFraudAlertRepository repository)
    : IRequestHandler<GetFraudAlertsQuery, IReadOnlyList<FraudAlertDto>>
{
    public async Task<IReadOnlyList<FraudAlertDto>> Handle(
        GetFraudAlertsQuery request, CancellationToken ct)
    {
        var from = DateTime.UtcNow.AddDays(-request.Days);
        var alerts = await repository.GetRecentAsync(from, request.Limit, ct);

        return alerts.Select(a => new FraudAlertDto(
            a.Id, a.OriginalDriverId, a.ReplacementDriverId,
            a.SwapCount, a.DetectedAt, a.IsAcknowledged)).ToList();
    }
}
