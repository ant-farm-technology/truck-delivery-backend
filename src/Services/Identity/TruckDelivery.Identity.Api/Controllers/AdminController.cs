using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruckDelivery.Identity.Application.Commands.RegisterUser;
using TruckDelivery.Identity.Domain.ValueObjects;

namespace TruckDelivery.Identity.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public sealed class AdminController(IMediator mediator) : ControllerBase
{
    /// <summary>Create a new admin account. Only existing admins can call this.</summary>
    [HttpPost("accounts")]
    public async Task<IActionResult> CreateAdminAccount([FromBody] CreateAdminRequest request, CancellationToken ct)
    {
        var command = new RegisterUserCommand(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            UserRole.Admin,
            request.PhoneNumber);

        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
        {
            return BadRequest(new { result.Error.Code, result.Error.Description });
        }

        return CreatedAtAction(nameof(CreateAdminAccount), new { userId = result.Value.UserId }, result.Value);
    }
}

public sealed record CreateAdminRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string PhoneNumber);
