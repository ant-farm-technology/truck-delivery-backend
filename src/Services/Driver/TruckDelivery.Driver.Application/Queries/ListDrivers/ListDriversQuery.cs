using Dapper;
using MediatR;
using System.Text;
using TruckDelivery.Driver.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence;

namespace TruckDelivery.Driver.Application.Queries.ListDrivers;

public sealed record ListDriversQuery(
    int? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<DriverDto>>;

public sealed class ListDriversQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IRequestHandler<ListDriversQuery, PagedResult<DriverDto>>
{
    public async Task<PagedResult<DriverDto>> Handle(ListDriversQuery request, CancellationToken ct)
    {
        using var conn = await dbConnectionFactory.CreateConnectionAsync(ct);

        var where = new StringBuilder("WHERE IsActive = 1");
        var p = new DynamicParameters();

        if (request.Status.HasValue)
        {
            where.Append(" AND Status = @Status");
            p.Add("Status", request.Status.Value);
        }

        var offset = (request.Page - 1) * request.PageSize;
        p.Add("Limit", request.PageSize);
        p.Add("Offset", offset);

        var countSql = $"SELECT COUNT(*) FROM driver.drivers {where}";
        var dataSql = $"""
            SELECT Id, Email, FirstName, LastName, PhoneNumber, LicenseNumber,
                   Status, CurrentVehicleId, IsActive, CreatedAt
            FROM driver.drivers {where}
            ORDER BY CreatedAt DESC
            LIMIT @Limit OFFSET @Offset
            """;

        var total = await conn.ExecuteScalarAsync<int>(countSql, p);
        var items = await conn.QueryAsync<DriverDto>(dataSql, p);

        return new PagedResult<DriverDto>(items.ToList(), total, request.Page, request.PageSize);
    }
}
