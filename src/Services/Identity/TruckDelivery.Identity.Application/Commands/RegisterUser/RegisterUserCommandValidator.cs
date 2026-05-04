using FluentValidation;
using TruckDelivery.Identity.Domain.ValueObjects;

namespace TruckDelivery.Identity.Application.Commands.RegisterUser;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required")
            .Matches(@"^(\+84|0)[3-9]\d{8}$").WithMessage("Invalid Vietnamese phone number format");

        When(x => x.Role == UserRole.Driver, () =>
        {
            RuleFor(x => x.DateOfBirth)
                .NotNull().WithMessage("Date of birth is required for drivers")
                .Must(dob => dob.HasValue && dob.Value <= DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-18)))
                .WithMessage("Driver must be at least 18 years old");
        });

        When(x => x.DateOfBirth.HasValue, () =>
        {
            RuleFor(x => x.DateOfBirth)
                .Must(dob => dob!.Value <= DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-16)))
                .WithMessage("User must be at least 16 years old");
        });
    }
}
