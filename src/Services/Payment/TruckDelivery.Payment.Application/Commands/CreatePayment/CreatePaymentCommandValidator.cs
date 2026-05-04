using FluentValidation;

namespace TruckDelivery.Payment.Application.Commands.CreatePayment;

public sealed class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Payment amount must be positive.");
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(10);
    }
}
