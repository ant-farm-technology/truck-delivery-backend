using System.Text.Json;
using MediatR;
using TruckDelivery.Order.Application.IntegrationEvents;
using TruckDelivery.Order.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Order.Application.Commands.CancelOrder;

public sealed class CancelOrderCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork,
    IOutboxRepository outboxRepository)
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

        var @event = new OrderCancelledEvent(order.Id, order.CustomerId, request.Reason);
        await outboxRepository.AddAsync(OutboxMessage.Create(
            eventType: nameof(OrderCancelledEvent),
            topic: "order.order.cancelled",
            partitionKey: order.Id.ToString(),
            payload: JsonSerializer.Serialize(@event)), ct);

        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}
