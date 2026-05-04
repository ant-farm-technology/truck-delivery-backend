using FluentValidation;
using TruckDelivery.Driver.Domain.ValueObjects;

namespace TruckDelivery.Driver.Application.Commands.RegisterDriver;

public sealed class RegisterDriverCommandValidator : AbstractValidator<RegisterDriverCommand>
{
    public RegisterDriverCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.LicenseNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LicenseGrade).IsInEnum()
            .Must(g => g != LicenseGrade.B1 && g != LicenseGrade.E)
            .WithMessage("License grade B1 and E are not eligible for freight transport.");
        RuleFor(x => x.LicenseExpiryDate).GreaterThan(DateOnly.FromDateTime(DateTime.UtcNow));
        RuleFor(x => x.DateOfBirth).LessThan(DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-18)));
        RuleFor(x => x.Address).NotEmpty().MaximumLength(300);
        RuleFor(x => x.IdCardNumber).NotEmpty().MaximumLength(20);
    }
}
