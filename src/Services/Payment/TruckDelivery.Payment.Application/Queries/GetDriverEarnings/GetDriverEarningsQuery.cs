using MediatR;
using TruckDelivery.Payment.Application.DTOs;

namespace TruckDelivery.Payment.Application.Queries.GetDriverEarnings;

public sealed record GetDriverEarningsQuery(
    Guid DriverId,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    int Page = 1,
    int PageSize = 20) : IRequest<DriverEarningsDto>;
