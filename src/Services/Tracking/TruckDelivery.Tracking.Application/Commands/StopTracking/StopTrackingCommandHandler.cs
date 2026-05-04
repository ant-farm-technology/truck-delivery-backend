using MediatR;
using TruckDelivery.Tracking.Domain.Repositories;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Tracking.Application.Commands.StopTracking;

public sealed class StopTrackingCommandHandler(
    ITrackingSessionRepository sessionRepository)
    : IRequestHandler<StopTrackingCommand, Result>
{
    public async Task<Result> Handle(StopTrackingCommand request, CancellationToken ct)
    {
        var session = await sessionRepository.GetActiveByShipmentIdAsync(request.ShipmentId, ct);
        if (session is null)
            return Result.Success(); // already stopped — idempotent

        session.Stop();
        await sessionRepository.UpdateAsync(session, ct);
        return Result.Success();
    }
}
