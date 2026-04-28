using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruckDelivery.Order.Application.Commands.CancelOrder;
using TruckDelivery.Order.Application.Commands.CreateOrder;
using TruckDelivery.Order.Application.DTOs;
using TruckDelivery.Order.Application.Queries.GetOrderById;
using TruckDelivery.Order.Application.Queries.ListOrdersByCustomer;

namespace TruckDelivery.Order.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/orders")]
public sealed class OrdersController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
            return BadRequest(new { result.Error.Code, result.Error.Description });

        return CreatedAtAction(nameof(GetOrderById), new { id = result.Value.OrderId }, result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrderById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetOrderByIdQuery(id), ct);
        if (result.IsFailure)
            return NotFound(new { result.Error.Code, result.Error.Description });

        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> ListByCustomer(
        [FromQuery] Guid customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListOrdersByCustomerQuery(customerId, page, pageSize), ct);
        if (result.IsFailure)
            return BadRequest(new { result.Error.Code, result.Error.Description });

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> CancelOrder(Guid id, [FromBody] CancelOrderRequest request, CancellationToken ct)
    {
        var requesterId = GetRequesterId();
        if (requesterId == Guid.Empty)
            return Unauthorized();

        var result = await mediator.Send(new CancelOrderCommand(id, requesterId, request.Reason), ct);
        if (result.IsFailure)
            return BadRequest(new { result.Error.Code, result.Error.Description });

        return NoContent();
    }

    private Guid GetRequesterId()
    {
        var sub = User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}

public sealed record CancelOrderRequest(string Reason);
