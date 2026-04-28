using MediatR;
using TruckDelivery.Order.Application.IntegrationEvents;
using TruckDelivery.Order.Domain.Repositories;
using TruckDelivery.Order.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Messaging;

namespace TruckDelivery.Order.Application.Commands.CreateOrder;

public sealed class CreateOrderCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork,
    IEventBus eventBus)
    : IRequestHandler<CreateOrderCommand, Result<CreateOrderResult>>
{
    public async Task<Result<CreateOrderResult>> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        var pickupResult = Address.Create(
            request.PickupAddress.Street,
            request.PickupAddress.City,
            request.PickupAddress.Province,
            request.PickupAddress.PostalCode,
            request.PickupAddress.Country);

        if (pickupResult.IsFailure)
            return Result.Failure<CreateOrderResult>(pickupResult.Error);

        var deliveryResult = Address.Create(
            request.DeliveryAddress.Street,
            request.DeliveryAddress.City,
            request.DeliveryAddress.Province,
            request.DeliveryAddress.PostalCode,
            request.DeliveryAddress.Country);

        if (deliveryResult.IsFailure)
            return Result.Failure<CreateOrderResult>(deliveryResult.Error);

        var items = request.Items
            .Select(i => (i.ProductName, i.Quantity, i.WeightKg, i.VolumeCbm, i.Notes))
            .ToList();

        var orderResult = Domain.Aggregates.Order.Create(
            request.CustomerId,
            pickupResult.Value,
            deliveryResult.Value,
            items,
            request.Notes);

        if (orderResult.IsFailure)
            return Result.Failure<CreateOrderResult>(orderResult.Error);

        var order = orderResult.Value;
        await orderRepository.AddAsync(order, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await eventBus.PublishAsync(new OrderCreatedEvent(
            order.Id,
            order.CustomerId,
            order.PickupAddress.City,
            order.PickupAddress.Province,
            order.DeliveryAddress.City,
            order.DeliveryAddress.Province,
            order.TotalWeightKg,
            order.TotalVolumeCbm), ct);

        return Result.Success(new CreateOrderResult(order.Id, order.CreatedAt));
    }
}
