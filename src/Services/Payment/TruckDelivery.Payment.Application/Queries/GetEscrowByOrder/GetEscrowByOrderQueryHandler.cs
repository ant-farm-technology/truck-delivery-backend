using System.Data;
using Dapper;
using MediatR;
using TruckDelivery.Payment.Application.DTOs;

namespace TruckDelivery.Payment.Application.Queries.GetEscrowByOrder;

public sealed class GetEscrowByOrderQueryHandler(IDbConnection db) : IRequestHandler<GetEscrowByOrderQuery, EscrowDto?>
{
    public async Task<EscrowDto?> Handle(GetEscrowByOrderQuery query, CancellationToken ct)
    {
        const string sql = """
            SELECT Id, ShipmentId, OrderId, OriginalDriverId, ReplacementDriverId,
                   LockedAmount, Currency, Status, ResolutionNote, LockedAt, ResolvedAt
            FROM EscrowPayments
            WHERE OrderId = @OrderId
            ORDER BY LockedAt DESC
            LIMIT 1
            """;

        return await db.QueryFirstOrDefaultAsync<EscrowDto>(sql, new { query.OrderId });
    }
}
