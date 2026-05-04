using System.Data;
using Dapper;
using MediatR;
using TruckDelivery.Payment.Application.DTOs;

namespace TruckDelivery.Payment.Application.Queries.GetPaymentByOrder;

public sealed class GetPaymentByOrderQueryHandler(IDbConnection db) : IRequestHandler<GetPaymentByOrderQuery, PaymentDto?>
{
    public async Task<PaymentDto?> Handle(GetPaymentByOrderQuery query, CancellationToken ct)
    {
        const string sql = """
            SELECT Id, OrderId, CustomerId, Amount, Currency, Status, FailureReason, CreatedAt
            FROM Payments
            WHERE OrderId = @OrderId
            LIMIT 1
            """;

        return await db.QueryFirstOrDefaultAsync<PaymentDto>(sql, new { query.OrderId });
    }
}
