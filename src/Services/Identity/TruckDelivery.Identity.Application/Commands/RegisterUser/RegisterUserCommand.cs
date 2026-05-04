using MediatR;
using TruckDelivery.Identity.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Identity.Application.Commands.RegisterUser;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    UserRole Role,
    string PhoneNumber,
    DateOnly? DateOfBirth = null) : IRequest<Result<RegisterUserResult>>;
