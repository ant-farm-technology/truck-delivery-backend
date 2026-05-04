using MediatR;
using TruckDelivery.Order.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Order.Application.Commands.SetOrderShipment;

public sealed class SetOrderShipmentCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SetOrderShipmentCommand, Result>
{
    public async Task<Result> Handle(SetOrderShipmentCommand request, CancellationToken ct)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, ct);
        if (order is null)
            return Result.Failure(Error.NotFound("Order", request.OrderId));

        order.SetShipmentId(request.ShipmentId);
        orderRepository.Update(order);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
