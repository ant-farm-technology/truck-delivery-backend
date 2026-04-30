using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TruckDelivery.Driver.Application.Interfaces;

namespace TruckDelivery.Driver.Api.Controllers;

[ApiController]
[Route("api/v1/uploads")]
[Produces("application/json")]
public sealed class UploadsController(IStorageService storageService) : ControllerBase
{
    private static readonly string[] DriverDocumentFields =
    [
        "portrait", "id-card-front", "id-card-back",
        "license-front", "license-back",
        "vehicle-reg-front", "vehicle-reg-back"
    ];

    [HttpGet("presigned-url")]
    [Authorize(Roles = "Driver")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetPresignedUrl(
        [FromQuery] string type,
        CancellationToken ct)
    {
        if (!type.Equals("driver-document", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = $"Unsupported upload type: {type}" });

        var driverId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var urls = await storageService.GenerateDriverDocumentUrlsAsync(driverId, DriverDocumentFields, ct);
        return Ok(new { urls });
    }
}
