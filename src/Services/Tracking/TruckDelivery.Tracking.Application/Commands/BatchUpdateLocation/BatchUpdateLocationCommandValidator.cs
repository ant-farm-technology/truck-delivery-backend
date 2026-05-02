using FluentValidation;

namespace TruckDelivery.Tracking.Application.Commands.BatchUpdateLocation;

public sealed class BatchUpdateLocationCommandValidator : AbstractValidator<BatchUpdateLocationCommand>
{
    private static readonly DateTime MaxAge = DateTime.UtcNow.AddDays(-1);

    public BatchUpdateLocationCommandValidator()
    {
        RuleFor(x => x.Points)
            .NotEmpty().WithMessage("Points list cannot be empty.")
            .Must(p => p.Count <= 100).WithMessage("Maximum 100 points per batch.");

        RuleForEach(x => x.Points).ChildRules(point =>
        {
            point.RuleFor(p => p.Latitude)
                .InclusiveBetween(-90, 90).WithMessage("Latitude must be between -90 and 90.");

            point.RuleFor(p => p.Longitude)
                .InclusiveBetween(-180, 180).WithMessage("Longitude must be between -180 and 180.");

            point.RuleFor(p => p.RecordedAt)
                .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("RecordedAt cannot be in the future.")
                .GreaterThan(DateTime.UtcNow.AddDays(-1)).WithMessage("RecordedAt cannot be older than 24 hours.");
        });
    }
}
