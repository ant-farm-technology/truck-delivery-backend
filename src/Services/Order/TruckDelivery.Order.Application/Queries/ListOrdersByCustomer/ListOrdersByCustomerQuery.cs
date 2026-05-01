using MediatR;
using TruckDelivery.Order.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Order.Application.Queries.ListOrdersByCustomer;

public sealed record ListOrdersByCustomerQuery(
    Guid CustomerId,
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null) : IRequest<Result<PagedResult<OrderSummaryDto>>>;
