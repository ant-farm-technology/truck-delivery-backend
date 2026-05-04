using MediatR;
using Microsoft.Extensions.Logging;
using TruckDelivery.Analytics.Domain.Repositories;

namespace TruckDelivery.Analytics.Application.Commands.RecordReassignmentCompleted;

public sealed class RecordReassignmentCompletedCommandHandler(
    IBreakdownIncidentRepository repository,
    ILogger<RecordReassignmentCompletedCommandHandler> logger)
    : IRequestHandler<RecordReassignmentCompletedCommand>
{
    public async Task Handle(RecordReassignmentCompletedCommand request, CancellationToken ct)
    {
        var incident = await repository.GetLatestUnresolvedByDriverIdAsync(request.OriginalDriverId, ct);
        if (incident is null)
        {
            logger.LogWarning("No unresolved breakdown incident found for Driver={DriverId}", request.OriginalDriverId);
            return;
        }

        incident.MarkResolved(request.ShipmentId, isReassigned: true);
        await repository.UpdateAsync(incident, ct);

        logger.LogInformation("Resolved breakdown incident {IncidentId} RecoveryTime={Minutes}min",
            incident.Id, incident.RecoveryTimeMinutes);
    }
}
