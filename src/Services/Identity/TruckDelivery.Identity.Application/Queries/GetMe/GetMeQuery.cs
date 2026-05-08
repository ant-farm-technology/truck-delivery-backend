using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Identity.Application.Queries.GetMe;

public sealed record UserProfileDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public sealed record GetMeQuery(Guid UserId) : IRequest<Result<UserProfileDto>>;
