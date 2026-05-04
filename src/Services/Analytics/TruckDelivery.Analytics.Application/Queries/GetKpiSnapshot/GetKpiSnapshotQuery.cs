using MediatR;
using TruckDelivery.Analytics.Application.DTOs;

namespace TruckDelivery.Analytics.Application.Queries.GetKpiSnapshot;

public sealed record GetKpiSnapshotQuery(int Days = 30) : IRequest<KpiSnapshotDto>;
