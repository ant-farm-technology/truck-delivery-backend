using MediatR;
using Microsoft.Extensions.Logging;
using TruckDelivery.Analytics.Domain.Documents;
using TruckDelivery.Analytics.Domain.Repositories;

namespace TruckDelivery.Analytics.Application.Commands.RecordBreakdown;

public sealed class RecordBreakdownCommandHandler(
    IBreakdownIncidentRepository repository,
    ILogger<RecordBreakdownCommandHandler> logger)
    : IRequestHandler<RecordBreakdownCommand>
{
    public async Task Handle(RecordBreakdownCommand request, CancellationToken ct)
    {
        var incident = BreakdownIncident.Create(
            request.DriverId, request.VehicleId,
            request.FraudRiskLevel, request.Latitude, request.Longitude);

        await repository.AddAsync(incident, ct);

        logger.LogInformation("Recorded breakdown incident {IncidentId} for Driver={DriverId} RiskLevel={RiskLevel}",
            incident.Id, request.DriverId, request.FraudRiskLevel);
    }
}
