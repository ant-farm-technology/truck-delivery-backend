using Dapper;
using MediatR;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence;

namespace TruckDelivery.Identity.Application.Queries.GetMe;

public sealed class GetMeQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IRequestHandler<GetMeQuery, Result<UserProfileDto>>
{
    public async Task<Result<UserProfileDto>> Handle(GetMeQuery request, CancellationToken ct)
    {
        using var conn = await dbConnectionFactory.CreateConnectionAsync(ct);

        const string sql = """
            SELECT Id, Email, FirstName, LastName, Role, PhoneNumber,
                   DateOfBirth, IsActive, CreatedAt, LastLoginAt
            FROM identity.users
            WHERE Id = @UserId
            """;

        var dto = await conn.QuerySingleOrDefaultAsync<UserProfileDto>(sql, new { request.UserId });

        return dto is null
            ? Result.Failure<UserProfileDto>(Error.NotFound("User", request.UserId))
            : Result.Success(dto);
    }
}
