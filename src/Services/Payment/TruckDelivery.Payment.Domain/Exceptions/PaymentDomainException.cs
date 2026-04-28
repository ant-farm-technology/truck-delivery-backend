namespace TruckDelivery.Payment.Domain.Exceptions;

public sealed class PaymentDomainException(string message) : Exception(message);
