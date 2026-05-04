using MediatR;
using TruckDelivery.Order.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Order.Application.Queries.GetOrderById;

public sealed record GetOrderByIdQuery(Guid OrderId) : IRequest<Result<OrderDto>>;
