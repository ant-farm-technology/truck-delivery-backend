namespace TruckDelivery.Shared.Common.Primitives;

public sealed record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "A null value was provided.");

    public static Error NotFound(string resource, object id) => new($"{resource}.NotFound", $"{resource} with id '{id}' was not found.");

    public static Error Conflict(string resource, string reason) => new($"{resource}.Conflict", reason);

    public static Error Validation(string property, string reason) => new($"Validation.{property}", reason);

    public static Error Unauthorized(string reason) => new("Auth.Unauthorized", reason);
}
