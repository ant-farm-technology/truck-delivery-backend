using MediatR;
using TruckDelivery.Analytics.Application.DTOs;

namespace TruckDelivery.Analytics.Application.Queries.GetFraudAlerts;

public sealed record GetFraudAlertsQuery(int Days = 30, int Limit = 50) : IRequest<IReadOnlyList<FraudAlertDto>>;
