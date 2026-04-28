using MediatR;
using TruckDelivery.Tracking.Domain.Aggregates;
using TruckDelivery.Tracking.Domain.Repositories;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Tracking.Application.Commands.StartTracking;

public sealed class StartTrackingCommandHandler(
    ITrackingSessionRepository sessionRepository)
    : IRequestHandler<StartTrackingCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(StartTrackingCommand request, CancellationToken ct)
    {
        var existing = await sessionRepository.GetActiveByShipmentIdAsync(request.ShipmentId, ct);
        if (existing is not null)
            return Result.Success(existing.Id);

        var session = TrackingSession.Start(request.ShipmentId, request.OrderId, request.DriverId);
        await sessionRepository.AddAsync(session, ct);
        return Result.Success(session.Id);
    }
}
