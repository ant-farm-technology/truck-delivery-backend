using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TruckDelivery.Identity.Application.Commands.Login;
using TruckDelivery.Identity.Application.Commands.RefreshToken;
using TruckDelivery.Identity.Application.Commands.RegisterUser;
using TruckDelivery.Identity.Application.Commands.RevokeRefreshToken;
using TruckDelivery.Identity.Application.Queries.GetMe;
using TruckDelivery.Identity.Domain.ValueObjects;

namespace TruckDelivery.Identity.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
    /// <summary>Register a new customer account.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterCustomer([FromBody] RegisterCustomerRequest request, CancellationToken ct)
    {
        var command = new RegisterUserCommand(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            UserRole.Customer,
            request.PhoneNumber,
            request.DateOfBirth);

        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
        {
            return BadRequest(new { result.Error.Code, result.Error.Description });
        }

        return CreatedAtAction(nameof(RegisterCustomer), new { userId = result.Value.UserId }, result.Value);
    }

    /// <summary>Register a new driver account (Step 1 of 3-step driver onboarding).</summary>
    [HttpPost("register/driver")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterDriver([FromBody] RegisterDriverRequest request, CancellationToken ct)
    {
        var command = new RegisterUserCommand(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            UserRole.Driver,
            request.PhoneNumber,
            request.DateOfBirth);

        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
        {
            return BadRequest(new { result.Error.Code, result.Error.Description });
        }

        return CreatedAtAction(nameof(RegisterDriver), new { userId = result.Value.UserId }, result.Value);
    }

    /// <summary>Authenticate and receive access + refresh tokens.</summary>
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

    /// <summary>Exchange a valid refresh token for new access + refresh tokens.</summary>
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

    /// <summary>Revoke the current user's refresh token. The JWT itself will expire naturally.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var sub = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized();

        await mediator.Send(new RevokeRefreshTokenCommand(userId), ct);
        return NoContent();
    }

    /// <summary>Returns the currently authenticated user's profile (Customer, Driver, or Admin).</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized();

        var result = await mediator.Send(new GetMeQuery(userId), ct);
        if (result.IsFailure)
            return NotFound(new { result.Error.Code, result.Error.Description });

        return Ok(result.Value);
    }
}

public sealed record RegisterCustomerRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string PhoneNumber,
    DateOnly? DateOfBirth = null);

public sealed record RegisterDriverRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string PhoneNumber,
    DateOnly DateOfBirth);

public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(Guid UserId, string RefreshToken);
