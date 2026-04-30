using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruckDelivery.Analytics.Application.Commands.AcknowledgeFraudAlert;
using TruckDelivery.Analytics.Application.DTOs;
using TruckDelivery.Analytics.Application.Queries.GetBreakdownIncidents;
using TruckDelivery.Analytics.Application.Queries.GetFraudAlerts;
using TruckDelivery.Analytics.Application.Queries.GetKpiSnapshot;

namespace TruckDelivery.Analytics.Api.Controllers;

[ApiController]
[Route("api/v1/analytics")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public sealed class AnalyticsController(IMediator mediator) : ControllerBase
{
    [HttpGet("kpis")]
    [ProducesResponseType(typeof(KpiSnapshotDto), 200)]
    public async Task<IActionResult> GetKpis([FromQuery] int days = 30, CancellationToken ct = default)
    {
        var snapshot = await mediator.Send(new GetKpiSnapshotQuery(days), ct);
        return Ok(snapshot);
    }

    [HttpGet("breakdown/incidents")]
    [ProducesResponseType(typeof(IReadOnlyList<BreakdownIncidentDto>), 200)]
    public async Task<IActionResult> GetBreakdownIncidents(
        [FromQuery] int days = 30, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var incidents = await mediator.Send(new GetBreakdownIncidentsQuery(days, limit), ct);
        return Ok(incidents);
    }

    [HttpGet("fraud/alerts")]
    [ProducesResponseType(typeof(IReadOnlyList<FraudAlertDto>), 200)]
    public async Task<IActionResult> GetFraudAlerts(
        [FromQuery] int days = 30, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var alerts = await mediator.Send(new GetFraudAlertsQuery(days, limit), ct);
        return Ok(alerts);
    }

    [HttpPost("fraud/alerts/{id:guid}/acknowledge")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AcknowledgeFraudAlert(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new AcknowledgeFraudAlertCommand(id), ct);
        if (result.IsFailure)
            return NotFound(new { result.Error.Code, result.Error.Description });

        return NoContent();
    }
}
