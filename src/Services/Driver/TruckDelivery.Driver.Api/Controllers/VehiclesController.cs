using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruckDelivery.Driver.Application.Commands.RegisterVehicle;
using TruckDelivery.Driver.Application.Queries.GetVehicleById;

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

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetVehicleById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetVehicleByIdQuery(id), ct);
        if (result.IsFailure)
            return NotFound(new { result.Error.Code, result.Error.Description });

        return Ok(result.Value);
    }
}
