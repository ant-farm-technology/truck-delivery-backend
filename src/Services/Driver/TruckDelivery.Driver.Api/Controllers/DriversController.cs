using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TruckDelivery.Driver.Application.Commands.AdminRejectDriver;
using TruckDelivery.Driver.Application.Commands.AdminVerifyDriver;
using TruckDelivery.Driver.Application.Commands.AssignVehicleToDriver;
using TruckDelivery.Driver.Application.Commands.RegisterDriver;
using TruckDelivery.Driver.Application.Commands.ReportBreakdown;
using TruckDelivery.Driver.Application.Commands.SelfRegisterDriver;
using TruckDelivery.Driver.Application.Commands.UpdateDriverStatus;
using TruckDelivery.Driver.Application.Queries.GetDriverById;
using TruckDelivery.Driver.Application.Queries.ListAvailableDrivers;
using TruckDelivery.Driver.Application.Queries.ListDrivers;
using TruckDelivery.Driver.Application.Queries.ListPendingVerification;
using TruckDelivery.Driver.Domain.ValueObjects;

namespace TruckDelivery.Driver.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/drivers")]
public sealed class DriversController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RegisterDriver([FromBody] RegisterDriverCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
            return BadRequest(new { result.Error.Code, result.Error.Description });

        return Created($"/api/v1/drivers/{command.UserId}", new { command.UserId });
    }

    // Step 3 of 3-step driver onboarding: submit profile + vehicle + document photo URLs
    [HttpPost("register")]
    [Authorize(Roles = "Driver")]
    [ProducesResponseType(typeof(SelfRegisterDriverResult), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> SelfRegister([FromBody] SelfRegisterDriverRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var email = User.FindFirstValue(ClaimTypes.Email) ?? "";
        var firstName = User.FindFirstValue(ClaimTypes.GivenName) ?? "";
        var lastName = User.FindFirstValue(ClaimTypes.Surname) ?? "";
        var phoneNumber = User.FindFirstValue("phone_number") ?? "";

        var command = new SelfRegisterDriverCommand(
            userId, email, firstName, lastName, phoneNumber,
            request.IdCardNumber, request.DateOfBirth, request.Address,
            request.LicenseNumber, request.LicenseGrade, request.LicenseExpiryDate,
            request.Photos.PortraitUrl, request.Photos.IdCardFrontUrl, request.Photos.IdCardBackUrl,
            request.Photos.LicenseFrontUrl, request.Photos.LicenseBackUrl,
            request.Photos.VehicleRegFrontUrl, request.Photos.VehicleRegBackUrl,
            request.Vehicle.LicensePlate, request.Vehicle.Brand, request.Vehicle.Model,
            request.Vehicle.Type, request.Vehicle.MaxWeightKg, request.Vehicle.MaxVolumeCbm,
            request.Vehicle.LengthM, request.Vehicle.WidthM, request.Vehicle.HeightM,
            request.Vehicle.YearOfManufacture, request.Vehicle.RegistrationNumber,
            request.Vehicle.RegistrationExpiryDate);

        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("Conflict")) return Conflict(new { result.Error.Code, result.Error.Description });
            return BadRequest(new { result.Error.Code, result.Error.Description });
        }

        return CreatedAtAction(nameof(GetDriverById), new { id = result.Value.DriverId }, result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDriverById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetDriverByIdQuery(id), ct);
        if (result.IsFailure)
            return NotFound(new { result.Error.Code, result.Error.Description });

        return Ok(result.Value);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ListDrivers(
        [FromQuery] int? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListDriversQuery(status, page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("available")]
    public async Task<IActionResult> ListAvailableDrivers(CancellationToken ct)
    {
        var result = await mediator.Send(new ListAvailableDriversQuery(), ct);
        return Ok(result.Value);
    }

    [HttpGet("pending-verification")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<PendingVerificationDto>), 200)]
    public async Task<IActionResult> ListPendingVerification(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListPendingVerificationQuery(page, pageSize), ct);
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/verify")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> VerifyDriver(Guid id, [FromBody] AdminVerifyRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new AdminVerifyDriverCommand(id, request.Notes), ct);
        if (result.IsFailure)
            return result.Error.Code.Contains("NotFound") ? NotFound(new { result.Error.Code, result.Error.Description }) : BadRequest(new { result.Error.Code, result.Error.Description });
        return NoContent();
    }

    [HttpPost("{id:guid}/reject-verification")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RejectVerification(Guid id, [FromBody] AdminRejectRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new AdminRejectDriverCommand(id, request.Reason), ct);
        if (result.IsFailure)
            return result.Error.Code.Contains("NotFound") ? NotFound(new { result.Error.Code, result.Error.Description }) : BadRequest(new { result.Error.Code, result.Error.Description });
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = "Admin,Driver")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateDriverStatusRequest request, CancellationToken ct)
    {
        var requestingUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var requestingUserRole = User.FindFirstValue(ClaimTypes.Role) ?? "";

        var result = await mediator.Send(new UpdateDriverStatusCommand(id, request.Status, requestingUserId, requestingUserRole), ct);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("Forbidden")) return Forbid();
            return BadRequest(new { result.Error.Code, result.Error.Description });
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/assign-vehicle")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignVehicle(Guid id, [FromBody] AssignVehicleRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new AssignVehicleToDriverCommand(request.VehicleId, id), ct);
        if (result.IsFailure)
            return BadRequest(new { result.Error.Code, result.Error.Description });

        return NoContent();
    }

    /// <summary>
    /// Driver reports vehicle breakdown. Passes through anti-fraud gate (GPS + photo + trust score).
    /// </summary>
    [HttpPost("{id:guid}/report-breakdown")]
    [Authorize(Roles = "Driver")]
    [ProducesResponseType(typeof(ReportBreakdownResult), 200)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> ReportBreakdown(Guid id, [FromBody] ReportBreakdownRequest request, CancellationToken ct)
    {
        var command = new ReportBreakdownCommand(id, request.Latitude, request.Longitude, request.PhotoUrls);
        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
            return UnprocessableEntity(new { result.Error.Code, result.Error.Description });

        return Ok(result.Value);
    }
}

public sealed record UpdateDriverStatusRequest(DriverStatus Status);
public sealed record AdminVerifyRequest(string? Notes = null);
public sealed record AdminRejectRequest(string Reason);
public sealed record AssignVehicleRequest(Guid VehicleId);
public sealed record ReportBreakdownRequest(double Latitude, double Longitude, IReadOnlyList<string> PhotoUrls);

public sealed record SelfRegisterDriverRequest(
    string IdCardNumber,
    DateOnly DateOfBirth,
    string Address,
    string LicenseNumber,
    LicenseGrade LicenseGrade,
    DateOnly LicenseExpiryDate,
    DriverPhotoUrls Photos,
    DriverVehicleRequest Vehicle);

public sealed record DriverPhotoUrls(
    string PortraitUrl,
    string IdCardFrontUrl,
    string IdCardBackUrl,
    string LicenseFrontUrl,
    string LicenseBackUrl,
    string VehicleRegFrontUrl,
    string VehicleRegBackUrl);

public sealed record DriverVehicleRequest(
    string LicensePlate,
    string Brand,
    string Model,
    VehicleType Type,
    decimal MaxWeightKg,
    decimal MaxVolumeCbm,
    decimal LengthM,
    decimal WidthM,
    decimal HeightM,
    int YearOfManufacture,
    string RegistrationNumber,
    DateOnly RegistrationExpiryDate);
