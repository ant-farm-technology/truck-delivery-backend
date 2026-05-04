using TruckDelivery.Payment.Domain.ValueObjects;

namespace TruckDelivery.Payment.Application.Interfaces;

public interface IPaymentGatewayFactory
{
    IPaymentGateway GetGateway(PaymentMethod method);
}
