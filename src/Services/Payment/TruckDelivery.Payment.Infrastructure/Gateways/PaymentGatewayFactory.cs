using TruckDelivery.Payment.Application.Interfaces;
using TruckDelivery.Payment.Domain.ValueObjects;

namespace TruckDelivery.Payment.Infrastructure.Gateways;

public sealed class PaymentGatewayFactory(
    VnPayGateway vnPayGateway,
    CodGateway codGateway) : IPaymentGatewayFactory
{
    public IPaymentGateway GetGateway(PaymentMethod method) => method switch
    {
        PaymentMethod.VnPay => vnPayGateway,
        PaymentMethod.Cod => codGateway,
        _ => codGateway
    };
}
