using MediatR;
using TruckDelivery.Driver.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Commands.ReportBreakdown;

public sealed record ReportBreakdownCommand(
    Guid DriverId,
    double Latitude,
    double Longitude,
    IReadOnlyList<string> PhotoUrls) : IRequest<Result<ReportBreakdownResult>>;

public sealed record ReportBreakdownResult(Guid ReportId, FraudRiskLevel FraudRiskLevel, bool Accepted);
