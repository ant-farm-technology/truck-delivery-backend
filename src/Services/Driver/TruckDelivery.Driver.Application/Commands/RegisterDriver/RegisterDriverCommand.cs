using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Commands.RegisterDriver;

public sealed record RegisterDriverCommand(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string LicenseNumber) : IRequest<Result>;
