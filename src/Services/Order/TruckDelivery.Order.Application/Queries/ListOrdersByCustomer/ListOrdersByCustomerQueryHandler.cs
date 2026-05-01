using Dapper;
using MediatR;
using TruckDelivery.Order.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence;

namespace TruckDelivery.Order.Application.Queries.ListOrdersByCustomer;

public sealed class ListOrdersByCustomerQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IRequestHandler<ListOrdersByCustomerQuery, Result<PagedResult<OrderSummaryDto>>>
{
    // Private row type to capture window function TotalCount alongside each row
    private sealed record OrderSummaryRow(
        Guid Id, Guid CustomerId, string Status, string PickupCity, string DeliveryCity,
        decimal TotalWeightKg, DateTime CreatedAt, Guid? ShipmentId, int TotalCount);

    public async Task<Result<PagedResult<OrderSummaryDto>>> Handle(ListOrdersByCustomerQuery request, CancellationToken ct)
    {
        using var conn = await dbConnectionFactory.CreateConnectionAsync(ct);

        var offset = (request.Page - 1) * request.PageSize;

        const string sql = """
            SELECT Id, CustomerId, Status, PickupCity, DeliveryCity, TotalWeightKg, CreatedAt, ShipmentId,
                   COUNT(*) OVER() AS TotalCount
            FROM `order`.orders
            WHERE CustomerId = @CustomerId
              AND (@Status IS NULL OR Status = @Status)
              AND (@DateFrom IS NULL OR CreatedAt >= @DateFrom)
              AND (@DateTo IS NULL OR CreatedAt <= @DateTo)
            ORDER BY CreatedAt DESC
            LIMIT @PageSize OFFSET @Offset
            """;

        var rows = (await conn.QueryAsync<OrderSummaryRow>(sql, new
        {
            request.CustomerId,
            request.Status,
            request.DateFrom,
            request.DateTo,
            request.PageSize,
            Offset = offset
        })).ToList();

        var totalCount = rows.FirstOrDefault()?.TotalCount ?? 0;
        var items = rows.Select(r => new OrderSummaryDto(
            r.Id, r.CustomerId, r.Status, r.PickupCity, r.DeliveryCity,
            r.TotalWeightKg, r.CreatedAt, r.ShipmentId)).ToList();

        return Result.Success(new PagedResult<OrderSummaryDto>(items, totalCount, request.Page, request.PageSize));
    }
}
