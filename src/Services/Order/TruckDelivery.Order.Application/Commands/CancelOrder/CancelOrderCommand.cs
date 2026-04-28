using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Order.Application.Commands.CancelOrder;

public sealed record CancelOrderCommand(Guid OrderId, Guid RequesterId, string Reason) : IRequest<Result>;
