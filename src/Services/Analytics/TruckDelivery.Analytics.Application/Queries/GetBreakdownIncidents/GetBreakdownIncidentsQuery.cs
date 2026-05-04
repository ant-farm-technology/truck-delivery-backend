using MediatR;
using TruckDelivery.Analytics.Application.DTOs;

namespace TruckDelivery.Analytics.Application.Queries.GetBreakdownIncidents;

public sealed record GetBreakdownIncidentsQuery(int Days = 30, int Limit = 50) : IRequest<IReadOnlyList<BreakdownIncidentDto>>;
