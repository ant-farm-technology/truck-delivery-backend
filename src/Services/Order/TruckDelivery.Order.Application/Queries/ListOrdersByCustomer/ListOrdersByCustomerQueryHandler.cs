using Dapper;
using MediatR;
using TruckDelivery.Order.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence;

namespace TruckDelivery.Order.Application.Queries.ListOrdersByCustomer;

public sealed class ListOrdersByCustomerQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IRequestHandler<ListOrdersByCustomerQuery, Result<IReadOnlyList<OrderSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<OrderSummaryDto>>> Handle(ListOrdersByCustomerQuery request, CancellationToken ct)
    {
        using var conn = await dbConnectionFactory.CreateConnectionAsync(ct);

        var offset = (request.Page - 1) * request.PageSize;

        const string sql = """
            SELECT Id, CustomerId, Status, PickupCity, DeliveryCity, TotalWeightKg, CreatedAt
            FROM `order`.orders
            WHERE CustomerId = @CustomerId
            ORDER BY CreatedAt DESC
            LIMIT @PageSize OFFSET @Offset
            """;

        var rows = await conn.QueryAsync<OrderSummaryDto>(sql, new
        {
            request.CustomerId,
            request.PageSize,
            Offset = offset
        });

        return Result.Success<IReadOnlyList<OrderSummaryDto>>(rows.ToList());
    }
}
