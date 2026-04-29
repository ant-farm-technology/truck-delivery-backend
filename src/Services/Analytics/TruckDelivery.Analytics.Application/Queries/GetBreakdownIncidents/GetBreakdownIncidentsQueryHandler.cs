using MediatR;
using TruckDelivery.Analytics.Application.DTOs;
using TruckDelivery.Analytics.Domain.Repositories;

namespace TruckDelivery.Analytics.Application.Queries.GetBreakdownIncidents;

public sealed class GetBreakdownIncidentsQueryHandler(IBreakdownIncidentRepository repository)
    : IRequestHandler<GetBreakdownIncidentsQuery, IReadOnlyList<BreakdownIncidentDto>>
{
    public async Task<IReadOnlyList<BreakdownIncidentDto>> Handle(
        GetBreakdownIncidentsQuery request, CancellationToken ct)
    {
        var from = DateTime.UtcNow.AddDays(-request.Days);
        var incidents = await repository.GetRecentAsync(from, request.Limit, ct);

        return incidents.Select(i => new BreakdownIncidentDto(
            i.Id, i.DriverId, i.VehicleId, i.ShipmentId,
            i.FraudRiskLevel, i.Latitude, i.Longitude,
            i.ReportedAt, i.ResolvedAt, i.IsResolved,
            i.IsSuccessfullyReassigned, i.RecoveryTimeMinutes)).ToList();
    }
}
