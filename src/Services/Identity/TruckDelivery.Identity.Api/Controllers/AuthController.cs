using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruckDelivery.Identity.Application.Commands.Login;
using TruckDelivery.Identity.Application.Commands.RefreshToken;
using TruckDelivery.Identity.Application.Commands.RegisterUser;

namespace TruckDelivery.Identity.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var command = new RegisterUserCommand(request.Email, request.Password, request.FirstName, request.LastName);
        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
        {
            return BadRequest(new { result.Error.Code, result.Error.Description });
        }

        return CreatedAtAction(nameof(Register), new { userId = result.Value.UserId }, result.Value);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
        {
            return Unauthorized(new { result.Error.Code, result.Error.Description });
        }

        return Ok(result.Value);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var command = new RefreshTokenCommand(request.UserId, request.RefreshToken);
        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
        {
            return Unauthorized(new { result.Error.Code, result.Error.Description });
        }

        return Ok(result.Value);
    }
}

public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(Guid UserId, string RefreshToken);
