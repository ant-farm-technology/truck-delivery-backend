using MediatR;
using TruckDelivery.Analytics.Application.DTOs;
using TruckDelivery.Analytics.Domain.Repositories;

namespace TruckDelivery.Analytics.Application.Queries.GetKpiSnapshot;

public sealed class GetKpiSnapshotQueryHandler(
    IBreakdownIncidentRepository incidentRepo,
    IFraudAlertRepository fraudRepo)
    : IRequestHandler<GetKpiSnapshotQuery, KpiSnapshotDto>
{
    public async Task<KpiSnapshotDto> Handle(GetKpiSnapshotQuery request, CancellationToken ct)
    {
        var from = DateTime.UtcNow.AddDays(-request.Days);

        var totalBreakdowns = await incidentRepo.CountAsync(from, ct);
        var successfulReassignments = await incidentRepo.CountSuccessfullyReassignedAsync(from, ct);
        var avgRecovery = await incidentRepo.AverageRecoveryTimeMinutesAsync(from, ct);
        var byRiskLevel = await incidentRepo.CountByRiskLevelAsync(from, ct);
        var fraudAlerts = await fraudRepo.CountAsync(from, ct);

        var successRate = totalBreakdowns == 0
            ? 0d
            : Math.Round((double)successfulReassignments / totalBreakdowns * 100, 1);

        return new KpiSnapshotDto(
            PeriodDays: request.Days,
            BreakdownCount: totalBreakdowns,
            SuccessfulReassignmentCount: successfulReassignments,
            ReassignmentSuccessRatePct: successRate,
            AvgRecoveryTimeMinutes: avgRecovery.HasValue ? Math.Round(avgRecovery.Value, 1) : null,
            FraudAlertCount: fraudAlerts,
            BreakdownsByRiskLevel: byRiskLevel.ToDictionary(x => x.RiskLevel, x => x.Count));
    }
}
