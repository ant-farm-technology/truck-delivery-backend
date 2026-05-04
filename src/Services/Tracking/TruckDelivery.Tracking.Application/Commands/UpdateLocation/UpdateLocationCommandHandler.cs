using System.Text.Json;
using MediatR;
using TruckDelivery.Tracking.Application.Interfaces;
using TruckDelivery.Tracking.Application.IntegrationEvents;
using TruckDelivery.Tracking.Domain.Aggregates;
using TruckDelivery.Tracking.Domain.Repositories;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Tracking.Application.Commands.UpdateLocation;

public sealed class UpdateLocationCommandHandler(
    ITrackingSessionRepository sessionRepository,
    ITrackingPointRepository pointRepository,
    IOutboxRepository outboxRepository,
    ITrackingNotifier notifier,
    IDriverGpsCache gpsCache)
    : IRequestHandler<UpdateLocationCommand, Result>
{
    public async Task<Result> Handle(UpdateLocationCommand request, CancellationToken ct)
    {
        var session = await sessionRepository.GetActiveByDriverIdAsync(request.DriverId, ct);
        if (session is null)
            return Result.Failure(Error.NotFound("TrackingSession", $"No active session for driver {request.DriverId}."));

        session.UpdateLocation(request.Latitude, request.Longitude);

        var point = new TrackingPoint
        {
            SessionId = session.Id,
            ShipmentId = session.ShipmentId,
            DriverId = request.DriverId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            SpeedKmh = request.SpeedKmh,
            HeadingDeg = request.HeadingDeg,
            RecordedAt = DateTime.UtcNow
        };

        await sessionRepository.UpdateAsync(session, ct);
        await pointRepository.AddAsync(point, ct);

        var @event = new LocationUpdatedEvent(
            session.ShipmentId,
            request.DriverId,
            request.Latitude,
            request.Longitude,
            request.SpeedKmh);

        await outboxRepository.AddAsync(OutboxMessage.Create(
            nameof(LocationUpdatedEvent),
            "tracking.location.updated",
            request.DriverId.ToString(),
            JsonSerializer.Serialize(@event)), ct);

        // Cache last known GPS for breakdown fraud gate — fire-and-forget, non-critical
        _ = gpsCache.SetAsync(request.DriverId, request.Latitude, request.Longitude, ct);

        // Fire-and-forget — don't block the response
        _ = notifier.NotifyLocationUpdatedAsync(
            session.ShipmentId, request.DriverId,
            request.Latitude, request.Longitude,
            request.SpeedKmh, point.RecordedAt, ct);

        return Result.Success();
    }
}
