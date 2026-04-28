using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruckDelivery.Payment.Application.Commands.CreatePayment;
using TruckDelivery.Payment.Application.DTOs;
using TruckDelivery.Payment.Application.Queries.GetPaymentByOrder;

namespace TruckDelivery.Payment.Api.Controllers;

[ApiController]
[Route("api/v1/payments")]
[Produces("application/json")]
public sealed class PaymentsController(IMediator mediator) : ControllerBase
{
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
}

public sealed record CreatePaymentRequest(Guid OrderId, Guid CustomerId, decimal Amount, string? Currency);
