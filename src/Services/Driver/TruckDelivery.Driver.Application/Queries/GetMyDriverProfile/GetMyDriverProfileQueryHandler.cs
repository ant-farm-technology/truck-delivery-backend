using Dapper;
using MediatR;
using TruckDelivery.Driver.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence;

namespace TruckDelivery.Driver.Application.Queries.GetMyDriverProfile;

public sealed class GetMyDriverProfileQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IRequestHandler<GetMyDriverProfileQuery, Result<DriverDto>>
{
    public async Task<Result<DriverDto>> Handle(GetMyDriverProfileQuery request, CancellationToken ct)
    {
        using var conn = await dbConnectionFactory.CreateConnectionAsync(ct);

        // Driver.Id == UserId (set at creation time)
        const string sql = """
            SELECT Id, Email, FirstName, LastName, PhoneNumber, LicenseNumber,
                   Status, VerificationStatus, LicenseGrade, TrustScore,
                   CurrentVehicleId, IsActive, CreatedAt
            FROM driver.drivers
            WHERE Id = @UserId
            """;

        var dto = await conn.QuerySingleOrDefaultAsync<DriverDto>(sql, new { request.UserId });

        return dto is null
            ? Result.Failure<DriverDto>(Error.NotFound("Driver", request.UserId))
            : Result.Success(dto);
    }
}
