using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruckDelivery.Driver.Application.Commands.AssignVehicleToDriver;
using TruckDelivery.Driver.Application.Commands.RegisterDriver;
using TruckDelivery.Driver.Application.Commands.ReportBreakdown;
using TruckDelivery.Driver.Application.Commands.UpdateDriverStatus;
using TruckDelivery.Driver.Application.Queries.GetDriverById;
using TruckDelivery.Driver.Application.Queries.ListAvailableDrivers;
using TruckDelivery.Driver.Domain.ValueObjects;

namespace TruckDelivery.Driver.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/drivers")]
public sealed class DriversController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RegisterDriver([FromBody] RegisterDriverCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
            return BadRequest(new { result.Error.Code, result.Error.Description });

        return Created($"/api/v1/drivers/{command.UserId}", new { command.UserId });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDriverById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetDriverByIdQuery(id), ct);
        if (result.IsFailure)
            return NotFound(new { result.Error.Code, result.Error.Description });

        return Ok(result.Value);
    }

    [HttpGet("available")]
    public async Task<IActionResult> ListAvailableDrivers(CancellationToken ct)
    {
        var result = await mediator.Send(new ListAvailableDriversQuery(), ct);
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateDriverStatusRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateDriverStatusCommand(id, request.Status), ct);
        if (result.IsFailure)
            return BadRequest(new { result.Error.Code, result.Error.Description });

        return NoContent();
    }

    [HttpPost("{id:guid}/assign-vehicle")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignVehicle(Guid id, [FromBody] AssignVehicleRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new AssignVehicleToDriverCommand(request.VehicleId, id), ct);
        if (result.IsFailure)
            return BadRequest(new { result.Error.Code, result.Error.Description });

        return NoContent();
    }

    /// <summary>
    /// Driver reports vehicle breakdown. Passes through anti-fraud gate (GPS + photo + trust score).
    /// </summary>
    [HttpPost("{id:guid}/report-breakdown")]
    [Authorize(Roles = "Driver")]
    [ProducesResponseType(typeof(ReportBreakdownResult), 200)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> ReportBreakdown(Guid id, [FromBody] ReportBreakdownRequest request, CancellationToken ct)
    {
        var command = new ReportBreakdownCommand(id, request.Latitude, request.Longitude, request.PhotoUrls);
        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
            return UnprocessableEntity(new { result.Error.Code, result.Error.Description });

        return Ok(result.Value);
    }
}

public sealed record UpdateDriverStatusRequest(DriverStatus Status);
public sealed record AssignVehicleRequest(Guid VehicleId);
public sealed record ReportBreakdownRequest(double Latitude, double Longitude, IReadOnlyList<string> PhotoUrls);
