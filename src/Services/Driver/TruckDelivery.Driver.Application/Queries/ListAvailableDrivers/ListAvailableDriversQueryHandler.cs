using Dapper;
using MediatR;
using TruckDelivery.Driver.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence;

namespace TruckDelivery.Driver.Application.Queries.ListAvailableDrivers;

public sealed class ListAvailableDriversQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IRequestHandler<ListAvailableDriversQuery, Result<IReadOnlyList<DriverSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<DriverSummaryDto>>> Handle(ListAvailableDriversQuery request, CancellationToken ct)
    {
        using var conn = await dbConnectionFactory.CreateConnectionAsync(ct);

        const string sql = """
            SELECT Id, CONCAT(FirstName, ' ', LastName) AS FullName, PhoneNumber, Status, CurrentVehicleId
            FROM driver.drivers
            WHERE Status = 2 AND IsActive = 1
            ORDER BY FirstName
            """;

        var rows = await conn.QueryAsync<DriverSummaryDto>(sql);
        return Result.Success<IReadOnlyList<DriverSummaryDto>>(rows.ToList());
    }
}
