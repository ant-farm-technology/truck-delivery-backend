using FluentValidation;

namespace TruckDelivery.Driver.Application.Commands.RegisterVehicle;

public sealed class RegisterVehicleCommandValidator : AbstractValidator<RegisterVehicleCommand>
{
    public RegisterVehicleCommandValidator()
    {
        RuleFor(x => x.LicensePlate).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Brand).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Model).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.MaxWeightKg).GreaterThan(0);
        RuleFor(x => x.MaxVolumeCbm).GreaterThan(0);
        RuleFor(x => x.YearOfManufacture).InclusiveBetween(2000, DateTime.UtcNow.Year + 1);
    }
}
