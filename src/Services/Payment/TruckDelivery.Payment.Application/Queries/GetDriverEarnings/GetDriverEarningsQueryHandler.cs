using System.Data;
using Dapper;
using MediatR;
using TruckDelivery.Payment.Application.DTOs;

namespace TruckDelivery.Payment.Application.Queries.GetDriverEarnings;

public sealed class GetDriverEarningsQueryHandler(IDbConnection db)
    : IRequestHandler<GetDriverEarningsQuery, DriverEarningsDto>
{
    public async Task<DriverEarningsDto> Handle(GetDriverEarningsQuery request, CancellationToken ct)
    {
        var p = new DynamicParameters();
        p.Add("DriverId", request.DriverId);

        var dateFilter = "";
        if (request.DateFrom.HasValue)
        {
            dateFilter += " AND CreatedAt >= @DateFrom";
            p.Add("DateFrom", request.DateFrom.Value);
        }
        if (request.DateTo.HasValue)
        {
            dateFilter += " AND CreatedAt <= @DateTo";
            p.Add("DateTo", request.DateTo.Value);
        }

        var offset = (request.Page - 1) * request.PageSize;
        p.Add("Limit", request.PageSize);
        p.Add("Offset", offset);

        var summaryRows = await db.QueryAsync<(decimal Amount, int TotalCount)>(
            $"SELECT COALESCE(SUM(Amount),0) AS Amount, COUNT(*) AS TotalCount FROM Payments WHERE DriverId = @DriverId AND Status = 'Completed'{dateFilter}",
            p);

        var summary = summaryRows.FirstOrDefault();

        var items = await db.QueryAsync<EarningItemDto>(
            $"""
            SELECT Id AS PaymentId, OrderId, Amount, Currency, Status, CreatedAt
            FROM Payments
            WHERE DriverId = @DriverId AND Status = 'Completed'{dateFilter}
            ORDER BY CreatedAt DESC
            LIMIT @Limit OFFSET @Offset
            """,
            p);

        return new DriverEarningsDto(
            request.DriverId,
            summary.Amount,
            summary.TotalCount,
            items.ToList(),
            request.Page,
            request.PageSize,
            summary.TotalCount);
    }
}
