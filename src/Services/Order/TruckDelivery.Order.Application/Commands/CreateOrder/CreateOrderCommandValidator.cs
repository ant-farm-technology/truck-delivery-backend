using FluentValidation;

namespace TruckDelivery.Order.Application.Commands.CreateOrder;

public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.PickupAddress).NotNull();
        RuleFor(x => x.PickupAddress.Street).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PickupAddress.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PickupAddress.Province).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PickupAddress.Latitude)
            .InclusiveBetween(-90, 90)
            .When(x => x.PickupAddress.Latitude.HasValue)
            .WithMessage("Pickup latitude must be between -90 and 90.");
        RuleFor(x => x.PickupAddress.Longitude)
            .InclusiveBetween(-180, 180)
            .When(x => x.PickupAddress.Longitude.HasValue)
            .WithMessage("Pickup longitude must be between -180 and 180.");
        RuleFor(x => x.DeliveryAddress).NotNull();
        RuleFor(x => x.DeliveryAddress.Street).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DeliveryAddress.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DeliveryAddress.Province).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DeliveryAddress.Latitude)
            .InclusiveBetween(-90, 90)
            .When(x => x.DeliveryAddress.Latitude.HasValue)
            .WithMessage("Delivery latitude must be between -90 and 90.");
        RuleFor(x => x.DeliveryAddress.Longitude)
            .InclusiveBetween(-180, 180)
            .When(x => x.DeliveryAddress.Longitude.HasValue)
            .WithMessage("Delivery longitude must be between -180 and 180.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Order must have at least one item.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductName).NotEmpty().MaximumLength(200);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.WeightKg).GreaterThan(0);
            item.RuleFor(i => i.VolumeCbm).GreaterThan(0);
        });
    }
}
