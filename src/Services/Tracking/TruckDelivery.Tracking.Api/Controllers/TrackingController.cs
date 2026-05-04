using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruckDelivery.Tracking.Application.Commands.BatchUpdateLocation;
using TruckDelivery.Tracking.Application.Commands.UpdateLocation;
using TruckDelivery.Tracking.Application.DTOs;
using TruckDelivery.Tracking.Domain.Repositories;

namespace TruckDelivery.Tracking.Api.Controllers;

[ApiController]
[Route("api/v1/tracking")]
[Produces("application/json")]
public sealed class TrackingController(IMediator mediator, ITrackingPointRepository pointRepository) : ControllerBase
{
    // Called by Driver app every 1-5 seconds
    [HttpPost("location")]
    [Authorize(Roles = "Driver")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UpdateLocation(
        [FromBody] UpdateLocationRequest request,
        CancellationToken ct)
    {
        var driverId = Guid.Parse(User.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("Missing driver ID in token"));

        var result = await mediator.Send(new UpdateLocationCommand(
            driverId,
            request.Latitude,
            request.Longitude,
            request.SpeedKmh,
            request.HeadingDeg), ct);

        return result.IsFailure ? BadRequest(result.Error.Description) : NoContent();
    }

    // Called by Driver app to flush offline-cached GPS points after reconnect (max 100 per call)
    [HttpPost("batch")]
    [Authorize(Roles = "Driver")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> BatchUpdateLocation(
        [FromBody] BatchUpdateLocationRequest request,
        CancellationToken ct)
    {
        var driverId = Guid.Parse(User.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("Missing driver ID in token"));

        var points = request.Points.Select(p => new LocationPointDto(
            p.Latitude, p.Longitude, p.SpeedKmh, p.HeadingDeg, p.RecordedAt)).ToList();

        var result = await mediator.Send(new BatchUpdateLocationCommand(driverId, points), ct);

        return result.IsFailure ? BadRequest(result.Error.Description) : NoContent();
    }

    // Returns recent GPS trail for a shipment
    [HttpGet("shipments/{shipmentId:guid}/points")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<TrackingPointDto>), 200)]
    public async Task<IActionResult> GetPoints(Guid shipmentId, [FromQuery] int limit = 100, CancellationToken ct = default)
    {
        var points = await pointRepository.GetByShipmentIdAsync(shipmentId, limit, ct);
        var dtos = points.Select(p => new TrackingPointDto(
            p.DriverId, p.Latitude, p.Longitude, p.SpeedKmh, p.RecordedAt));
        return Ok(dtos);
    }
}

public sealed record UpdateLocationRequest(
    double Latitude,
    double Longitude,
    double? SpeedKmh = null,
    double? HeadingDeg = null);

public sealed record BatchLocationPoint(
    double Latitude,
    double Longitude,
    DateTime RecordedAt,
    double? SpeedKmh = null,
    double? HeadingDeg = null);

public sealed record BatchUpdateLocationRequest(IReadOnlyList<BatchLocationPoint> Points);
