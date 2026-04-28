using MediatR;
using TruckDelivery.Order.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Order.Application.Commands.CreateOrder;

public sealed record CreateOrderCommand(
    Guid CustomerId,
    AddressRequest PickupAddress,
    AddressRequest DeliveryAddress,
    IReadOnlyList<OrderItemRequest> Items,
    string? Notes = null) : IRequest<Result<CreateOrderResult>>;

public sealed record CreateOrderResult(Guid OrderId, DateTime CreatedAt);
