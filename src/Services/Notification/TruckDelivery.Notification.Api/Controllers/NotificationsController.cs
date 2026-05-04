using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TruckDelivery.Notification.Application.Commands.RegisterDevice;

namespace TruckDelivery.Notification.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Produces("application/json")]
[Authorize]
public sealed class NotificationsController(IMediator mediator) : ControllerBase
{
    [HttpPost("register-device")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await mediator.Send(new RegisterDeviceCommand(userId, request.Token, request.Platform), ct);
        return result.IsFailure ? BadRequest(new { result.Error.Code, result.Error.Description }) : NoContent();
    }
}

public sealed record RegisterDeviceRequest(string Token, string Platform);
