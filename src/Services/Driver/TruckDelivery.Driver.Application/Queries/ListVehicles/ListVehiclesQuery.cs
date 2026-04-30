using Dapper;
using MediatR;
using System.Text;
using TruckDelivery.Driver.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence;

namespace TruckDelivery.Driver.Application.Queries.ListVehicles;

public sealed record ListVehiclesQuery(
    int? Status = null,
    Guid? DriverId = null,
    int? Type = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<VehicleDto>>;

public sealed class ListVehiclesQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IRequestHandler<ListVehiclesQuery, PagedResult<VehicleDto>>
{
    public async Task<PagedResult<VehicleDto>> Handle(ListVehiclesQuery request, CancellationToken ct)
    {
        using var conn = await dbConnectionFactory.CreateConnectionAsync(ct);

        var where = new StringBuilder("WHERE 1=1");
        var p = new DynamicParameters();

        if (request.Status.HasValue)
        {
            where.Append(" AND Status = @Status");
            p.Add("Status", request.Status.Value);
        }

        if (request.DriverId.HasValue)
        {
            where.Append(" AND AssignedDriverId = @DriverId");
            p.Add("DriverId", request.DriverId.Value);
        }

        if (request.Type.HasValue)
        {
            where.Append(" AND Type = @Type");
            p.Add("Type", request.Type.Value);
        }

        var offset = (request.Page - 1) * request.PageSize;
        p.Add("Limit", request.PageSize);
        p.Add("Offset", offset);

        var countSql = $"SELECT COUNT(*) FROM driver.vehicles {where}";
        var dataSql = $"""
            SELECT Id, LicensePlate, Brand, Model, Type, MaxWeightKg,
                   MaxVolumeCbm, YearOfManufacture, Status, AssignedDriverId, CreatedAt
            FROM driver.vehicles {where}
            ORDER BY CreatedAt DESC
            LIMIT @Limit OFFSET @Offset
            """;

        var total = await conn.ExecuteScalarAsync<int>(countSql, p);
        var items = await conn.QueryAsync<VehicleDto>(dataSql, p);

        return new PagedResult<VehicleDto>(items.ToList(), total, request.Page, request.PageSize);
    }
}
