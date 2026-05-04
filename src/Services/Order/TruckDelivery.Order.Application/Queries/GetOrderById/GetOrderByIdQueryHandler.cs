using Dapper;
using MediatR;
using TruckDelivery.Order.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence;

namespace TruckDelivery.Order.Application.Queries.GetOrderById;

public sealed class GetOrderByIdQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IRequestHandler<GetOrderByIdQuery, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(GetOrderByIdQuery request, CancellationToken ct)
    {
        using var conn = await dbConnectionFactory.CreateConnectionAsync(ct);

        const string orderSql = """
            SELECT
                o.Id, o.CustomerId, o.Status,
                o.PickupStreet, o.PickupCity, o.PickupProvince,
                o.DeliveryStreet, o.DeliveryCity, o.DeliveryProvince,
                o.TotalWeightKg, o.TotalVolumeCbm,
                o.Notes, o.CancellationReason,
                o.CreatedAt, o.UpdatedAt, o.ShipmentId
            FROM `order`.orders o
            WHERE o.Id = @OrderId
            """;

        const string itemsSql = """
            SELECT Id, ProductName, Quantity, WeightKg, VolumeCbm, Notes
            FROM `order`.order_items
            WHERE OrderId = @OrderId
            """;

        var orderRow = await conn.QuerySingleOrDefaultAsync(orderSql, new { request.OrderId });
        if (orderRow is null)
            return Result.Failure<OrderDto>(Error.NotFound("Order", request.OrderId));

        var items = await conn.QueryAsync<OrderItemDto>(itemsSql, new { request.OrderId });

        var dto = new OrderDto(
            orderRow.Id,
            orderRow.CustomerId,
            orderRow.Status,
            orderRow.PickupStreet,
            orderRow.PickupCity,
            orderRow.PickupProvince,
            orderRow.DeliveryStreet,
            orderRow.DeliveryCity,
            orderRow.DeliveryProvince,
            orderRow.TotalWeightKg,
            orderRow.TotalVolumeCbm,
            orderRow.Notes,
            orderRow.CancellationReason,
            orderRow.CreatedAt,
            orderRow.UpdatedAt,
            items.ToList(),
            orderRow.ShipmentId);

        return Result.Success(dto);
    }
}
