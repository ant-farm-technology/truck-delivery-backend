using System.Text.Json;
using MediatR;
using TruckDelivery.Tracking.Application.Interfaces;
using TruckDelivery.Tracking.Application.IntegrationEvents;
using TruckDelivery.Tracking.Domain.Aggregates;
using TruckDelivery.Tracking.Domain.Repositories;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Tracking.Application.Commands.BatchUpdateLocation;

public sealed class BatchUpdateLocationCommandHandler(
    ITrackingSessionRepository sessionRepository,
    ITrackingPointRepository pointRepository,
    IOutboxRepository outboxRepository,
    ITrackingNotifier notifier,
    IDriverGpsCache gpsCache)
    : IRequestHandler<BatchUpdateLocationCommand, Result>
{
    public async Task<Result> Handle(BatchUpdateLocationCommand request, CancellationToken ct)
    {
        var session = await sessionRepository.GetActiveByDriverIdAsync(request.DriverId, ct);
        if (session is null)
            return Result.Failure(Error.NotFound("TrackingSession", $"No active session for driver {request.DriverId}."));

        // Sort by client timestamp ASC; take the most recent for session + real-time notifications
        var sorted = request.Points.OrderBy(p => p.RecordedAt).ToList();
        var lastPoint = sorted[^1];

        var trackingPoints = sorted.Select(p => new TrackingPoint
        {
            SessionId = session.Id,
            ShipmentId = session.ShipmentId,
            DriverId = request.DriverId,
            Latitude = p.Latitude,
            Longitude = p.Longitude,
            SpeedKmh = p.SpeedKmh,
            HeadingDeg = p.HeadingDeg,
            RecordedAt = p.RecordedAt   // preserve original client timestamp
        }).ToList();

        await pointRepository.AddManyAsync(trackingPoints, ct);

        // Update session last-known position with the most recent point only
        session.UpdateLocation(lastPoint.Latitude, lastPoint.Longitude);
        await sessionRepository.UpdateAsync(session, ct);

        // Cache most recent GPS for breakdown fraud gate
        _ = gpsCache.SetAsync(request.DriverId, lastPoint.Latitude, lastPoint.Longitude, ct);

        // Publish single Kafka event for the most recent point — batch is historical catch-up
        var @event = new LocationUpdatedEvent(
            session.ShipmentId,
            request.DriverId,
            lastPoint.Latitude,
            lastPoint.Longitude,
            lastPoint.SpeedKmh);

        await outboxRepository.AddAsync(OutboxMessage.Create(
            nameof(LocationUpdatedEvent),
            "tracking.location.updated",
            request.DriverId.ToString(),
            JsonSerializer.Serialize(@event)), ct);

        // Notify SignalR with the most recent point only
        _ = notifier.NotifyLocationUpdatedAsync(
            session.ShipmentId, request.DriverId,
            lastPoint.Latitude, lastPoint.Longitude,
            lastPoint.SpeedKmh, lastPoint.RecordedAt, ct);

        return Result.Success();
    }
}
