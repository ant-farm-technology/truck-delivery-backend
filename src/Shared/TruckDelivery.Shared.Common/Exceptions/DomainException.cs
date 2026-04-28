using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Shared.Common.Exceptions;

public class DomainException(Error error) : Exception(error.Description)
{
    public Error Error { get; } = error;
}
