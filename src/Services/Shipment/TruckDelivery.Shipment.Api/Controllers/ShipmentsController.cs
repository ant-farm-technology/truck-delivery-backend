using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruckDelivery.Shipment.Application.Commands.ConfirmDispatch;
using TruckDelivery.Shipment.Application.Commands.FailShipment;
using TruckDelivery.Shipment.Application.Commands.UpdateShipmentStatus;
using TruckDelivery.Shipment.Application.DTOs;
using TruckDelivery.Shipment.Domain.Aggregates;
using TruckDelivery.Shipment.Infrastructure.Persistence.Dapper;

namespace TruckDelivery.Shipment.Api.Controllers;

[ApiController]
[Route("api/v1/shipments")]
[Produces("application/json")]
public sealed class ShipmentsController(IMediator mediator, ShipmentQueryRepository queryRepository) : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(PagedResult<ShipmentDto>), 200)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] Guid? customerId,
        [FromQuery] Guid? driverId,
        [FromQuery] Guid? orderId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await queryRepository.ListAsync(status, customerId, driverId, orderId, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ShipmentDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await queryRepository.GetByIdAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("{id:guid}/confirm-dispatch")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ConfirmDispatch(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new ConfirmDispatchCommand(id), ct);
        if (result.IsFailure) return result.Error.Code.Contains("NotFound") ? NotFound(result.Error.Description) : BadRequest(result.Error.Description);
        return NoContent();
    }

    [HttpPost("{id:guid}/decline-dispatch")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeclineDispatch(Guid id, [FromBody] DeclineDispatchRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new FailShipmentCommand(id, request.Reason), ct);
        if (result.IsFailure)
            return result.Error.Code.Contains("NotFound") ? NotFound(result.Error.Description) : BadRequest(result.Error.Description);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = "Admin,Driver")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<ShipmentStatus>(request.Status, ignoreCase: true, out var status))
            return BadRequest($"Invalid status: {request.Status}");

        var result = await mediator.Send(new UpdateShipmentStatusCommand(id, status), ct);
        return result.IsFailure ? BadRequest(result.Error.Description) : NoContent();
    }
}

public sealed record UpdateStatusRequest(string Status);
public sealed record DeclineDispatchRequest(string Reason);
