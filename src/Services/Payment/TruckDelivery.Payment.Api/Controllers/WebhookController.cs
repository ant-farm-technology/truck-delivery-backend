using MediatR;
using Microsoft.AspNetCore.Mvc;
using TruckDelivery.Payment.Application.Commands.HandleVnPayCallback;

namespace TruckDelivery.Payment.Api.Controllers;

[ApiController]
[Route("api/v1/payments/webhook")]
public sealed class WebhookController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// VNPay IPN / return URL handler. VNPay calls this with GET query params after payment.
    /// </summary>
    [HttpGet("vnpay")]
    [HttpPost("vnpay")]
    public async Task<IActionResult> VnPayCallback(CancellationToken ct)
    {
        var queryParams = HttpContext.Request.Query
            .ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

        var result = await mediator.Send(new HandleVnPayCallbackCommand(queryParams), ct);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error.Description });

        // VNPay IPN expects "RspCode":"00" on success
        return result.Value.IsSuccess
            ? Ok(new { RspCode = "00", Message = "Confirmed" })
            : Ok(new { RspCode = "01", Message = result.Value.FailureReason });
    }
}
