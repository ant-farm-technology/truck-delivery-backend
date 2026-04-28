using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Shared.Common.Exceptions;

public class NotFoundException(string resource, object id) : Exception($"{resource} with id '{id}' was not found.")
{
    public Error Error { get; } = Error.NotFound(resource, id);
}
