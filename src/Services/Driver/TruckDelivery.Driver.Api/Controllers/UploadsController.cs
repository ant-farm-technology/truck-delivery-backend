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
        [FromQuery] int count = 5,
        CancellationToken ct = default)
    {
        var driverId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (type.Equals("driver-document", StringComparison.OrdinalIgnoreCase))
        {
            var urls = await storageService.GenerateDriverDocumentUrlsAsync(driverId, DriverDocumentFields, ct);
            return Ok(new { urls });
        }

        if (type.Equals("breakdown-photo", StringComparison.OrdinalIgnoreCase))
        {
            if (count is < 1 or > 10)
                return BadRequest(new { error = "count must be between 1 and 10" });

            var urls = await storageService.GenerateBreakdownPhotoUrlsAsync(driverId, count, ct);
            return Ok(new { urls });
        }

        return BadRequest(new { error = $"Unsupported upload type: {type}. Supported: driver-document, breakdown-photo" });
    }
}
