namespace TruckDelivery.Shared.Common.Primitives;

public static class Guard
{
    public static T AgainstNull<T>(T? value, string paramName) where T : class =>
        value ?? throw new ArgumentNullException(paramName);

    public static string AgainstNullOrWhiteSpace(string? value, string paramName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} cannot be null or whitespace.", paramName)
            : value;

    public static Guid AgainstEmptyGuid(Guid value, string paramName) =>
        value == Guid.Empty
            ? throw new ArgumentException($"{paramName} cannot be an empty Guid.", paramName)
            : value;

    public static int AgainstNegativeOrZero(int value, string paramName) =>
        value <= 0
            ? throw new ArgumentOutOfRangeException(paramName, $"{paramName} must be greater than zero.")
            : value;

    public static decimal AgainstNegative(decimal value, string paramName) =>
        value < 0
            ? throw new ArgumentOutOfRangeException(paramName, $"{paramName} cannot be negative.")
            : value;

    public static T AgainstDefault<T>(T value, string paramName) where T : struct =>
        value.Equals(default(T))
            ? throw new ArgumentException($"{paramName} cannot be the default value.", paramName)
            : value;
}
