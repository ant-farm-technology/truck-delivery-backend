using MediatR;
using TruckDelivery.Driver.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Commands.RegisterDriver;

public sealed record RegisterDriverCommand(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string LicenseNumber,
    LicenseGrade LicenseGrade,
    DateOnly LicenseExpiryDate,
    DateOnly DateOfBirth,
    string Address,
    string IdCardNumber) : IRequest<Result>;
