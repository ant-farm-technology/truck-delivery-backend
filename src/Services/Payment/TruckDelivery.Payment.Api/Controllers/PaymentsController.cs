using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruckDelivery.Payment.Application.Commands.CreatePayment;
using TruckDelivery.Payment.Application.Commands.ResolveEscrow;
using TruckDelivery.Payment.Application.DTOs;
using TruckDelivery.Payment.Application.Queries.GetEscrowByOrder;
using TruckDelivery.Payment.Application.Queries.GetPaymentByOrder;
using TruckDelivery.Payment.Application.Queries.ListPayments;

namespace TruckDelivery.Payment.Api.Controllers;

[ApiController]
[Route("api/v1/payments")]
[Produces("application/json")]
public sealed class PaymentsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> ListPayments(
        [FromQuery] string? status,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListPaymentsQuery(status, dateFrom, dateTo, page, pageSize), ct);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(object), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreatePaymentRequest request, CancellationToken ct)
    {
        var command = new CreatePaymentCommand(request.OrderId, request.CustomerId, request.Amount, request.Currency ?? "VND");
        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
            return Conflict(new { error = result.Error.Description });
        return CreatedAtAction(nameof(GetByOrder), new { orderId = request.OrderId }, new { paymentId = result.Value });
    }

    [HttpGet("orders/{orderId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(PaymentDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetByOrder(Guid orderId, CancellationToken ct)
    {
        var dto = await mediator.Send(new GetPaymentByOrderQuery(orderId), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("orders/{orderId:guid}/escrow")]
    [Authorize]
    [ProducesResponseType(typeof(EscrowDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetEscrowByOrder(Guid orderId, CancellationToken ct)
    {
        var dto = await mediator.Send(new GetEscrowByOrderQuery(orderId), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("escrow/{id:guid}/confirm")]
    [Authorize(Roles = "Customer,Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> ConfirmEscrow(Guid id, [FromBody] EscrowNoteRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new ResolveEscrowCommand(id, EscrowResolution.Confirm, request.Note), ct);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound")) return NotFound(new { error = result.Error.Description });
            return Conflict(new { error = result.Error.Description });
        }
        return NoContent();
    }

    [HttpPost("escrow/{id:guid}/dispute")]
    [Authorize(Roles = "Customer,Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> DisputeEscrow(Guid id, [FromBody] EscrowNoteRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new ResolveEscrowCommand(id, EscrowResolution.Dispute, request.Note), ct);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound")) return NotFound(new { error = result.Error.Description });
            return Conflict(new { error = result.Error.Description });
        }
        return NoContent();
    }
}

public sealed record CreatePaymentRequest(Guid OrderId, Guid CustomerId, decimal Amount, string? Currency);
public sealed record EscrowNoteRequest(string? Note);
