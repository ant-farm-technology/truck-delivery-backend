using System.Data;
using System.Text;
using Dapper;
using MediatR;
using TruckDelivery.Payment.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Payment.Application.Queries.ListPayments;

public sealed record ListPaymentsQuery(
    string? Status = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<PaymentDto>>;

public sealed class ListPaymentsQueryHandler(IDbConnection db)
    : IRequestHandler<ListPaymentsQuery, PagedResult<PaymentDto>>
{
    public async Task<PagedResult<PaymentDto>> Handle(ListPaymentsQuery request, CancellationToken ct)
    {
        var where = new StringBuilder("WHERE 1=1");
        var p = new DynamicParameters();

        if (!string.IsNullOrEmpty(request.Status))
        {
            where.Append(" AND Status = @Status");
            p.Add("Status", request.Status);
        }

        if (request.DateFrom.HasValue)
        {
            where.Append(" AND CreatedAt >= @DateFrom");
            p.Add("DateFrom", request.DateFrom.Value);
        }

        if (request.DateTo.HasValue)
        {
            where.Append(" AND CreatedAt <= @DateTo");
            p.Add("DateTo", request.DateTo.Value);
        }

        var offset = (request.Page - 1) * request.PageSize;
        p.Add("Limit", request.PageSize);
        p.Add("Offset", offset);

        var countSql = $"SELECT COUNT(*) FROM Payments {where}";
        var dataSql = $"""
            SELECT Id, OrderId, CustomerId, Amount, Currency, Status, FailureReason, CreatedAt
            FROM Payments {where}
            ORDER BY CreatedAt DESC
            LIMIT @Limit OFFSET @Offset
            """;

        var total = await db.ExecuteScalarAsync<int>(countSql, p);
        var items = await db.QueryAsync<PaymentDto>(dataSql, p);

        return new PagedResult<PaymentDto>(items.ToList(), total, request.Page, request.PageSize);
    }
}
