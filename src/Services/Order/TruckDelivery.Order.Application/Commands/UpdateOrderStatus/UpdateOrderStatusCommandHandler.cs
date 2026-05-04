using MediatR;
using TruckDelivery.Order.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Order.Application.Commands.UpdateOrderStatus;

public sealed class UpdateOrderStatusCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateOrderStatusCommand, Result>
{
    public async Task<Result> Handle(UpdateOrderStatusCommand request, CancellationToken ct)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, ct);
        if (order is null)
            return Result.Failure(Error.NotFound("Order", request.OrderId));

        var result = order.UpdateStatus(request.NewStatus);
        if (result.IsFailure)
            return result;

        orderRepository.Update(order);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
