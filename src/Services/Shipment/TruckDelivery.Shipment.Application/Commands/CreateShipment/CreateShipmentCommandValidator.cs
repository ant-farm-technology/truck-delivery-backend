using FluentValidation;

namespace TruckDelivery.Shipment.Application.Commands.CreateShipment;

public sealed class CreateShipmentCommandValidator : AbstractValidator<CreateShipmentCommand>
{
    public CreateShipmentCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.PickupCity).NotEmpty();
        RuleFor(x => x.DeliveryCity).NotEmpty();
        RuleFor(x => x.TotalWeightKg).GreaterThan(0);
        RuleFor(x => x.TotalVolumeCbm).GreaterThan(0);
    }
}
