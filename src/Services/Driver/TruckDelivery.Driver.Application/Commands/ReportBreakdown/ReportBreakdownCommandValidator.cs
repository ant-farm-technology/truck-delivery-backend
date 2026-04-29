using FluentValidation;

namespace TruckDelivery.Driver.Application.Commands.ReportBreakdown;

public sealed class ReportBreakdownCommandValidator : AbstractValidator<ReportBreakdownCommand>
{
    public ReportBreakdownCommandValidator()
    {
        RuleFor(x => x.DriverId).NotEmpty();
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.PhotoUrls)
            .NotEmpty()
            .WithMessage("At least one photo is required to report a breakdown.");
        RuleFor(x => x.PhotoUrls.Count)
            .GreaterThanOrEqualTo(1)
            .WithMessage("At least one photo is required.");
    }
}
