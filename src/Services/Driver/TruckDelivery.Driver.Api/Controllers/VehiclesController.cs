using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruckDelivery.Driver.Application.Commands.RegisterVehicle;
using TruckDelivery.Driver.Application.Commands.UpdateVehicleStatus;
using TruckDelivery.Driver.Application.Queries.GetVehicleById;
using TruckDelivery.Driver.Application.Queries.ListVehicles;
using TruckDelivery.Driver.Domain.ValueObjects;

namespace TruckDelivery.Driver.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/vehicles")]
public sealed class VehiclesController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RegisterVehicle([FromBody] RegisterVehicleCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
            return BadRequest(new { result.Error.Code, result.Error.Description });

        return CreatedAtAction(nameof(GetVehicleById), new { id = result.Value }, new { VehicleId = result.Value });
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ListVehicles(
        [FromQuery] int? status,
        [FromQuery] Guid? driverId,
        [FromQuery] int? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListVehiclesQuery(status, driverId, type, page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetVehicleById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetVehicleByIdQuery(id), ct);
        if (result.IsFailure)
            return NotFound(new { result.Error.Code, result.Error.Description });

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateVehicleStatus(Guid id, [FromBody] UpdateVehicleStatusRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateVehicleStatusCommand(id, request.Status), ct);
        if (result.IsFailure)
            return result.Error.Code.Contains("NotFound")
                ? NotFound(new { result.Error.Code, result.Error.Description })
                : BadRequest(new { result.Error.Code, result.Error.Description });

        return NoContent();
    }
}

public sealed record UpdateVehicleStatusRequest(VehicleStatus Status);
