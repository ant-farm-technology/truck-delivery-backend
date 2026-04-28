using MediatR;
using TruckDelivery.Order.Application.IntegrationEvents;
using TruckDelivery.Order.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Messaging;

namespace TruckDelivery.Order.Application.Commands.CancelOrder;

public sealed class CancelOrderCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork,
    IEventBus eventBus)
    : IRequestHandler<CancelOrderCommand, Result>
{
    public async Task<Result> Handle(CancelOrderCommand request, CancellationToken ct)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, ct);
        if (order is null)
            return Result.Failure(Error.NotFound("Order", request.OrderId));

        if (order.CustomerId != request.RequesterId)
            return Result.Failure(Error.Unauthorized("You are not authorized to cancel this order."));

        var cancelResult = order.Cancel(request.Reason);
        if (cancelResult.IsFailure)
            return cancelResult;

        orderRepository.Update(order);
        await unitOfWork.SaveChangesAsync(ct);

        await eventBus.PublishAsync(new OrderCancelledEvent(order.Id, order.CustomerId, request.Reason), ct);

        return Result.Success();
    }
}
