using TruckDelivery.Shared.Common.Domain;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Order.Domain.ValueObjects;

public sealed class Address : ValueObject
{
    public string Street { get; }
    public string City { get; }
    public string Province { get; }
    public string PostalCode { get; }
    public string Country { get; }

    private Address(string street, string city, string province, string postalCode, string country)
    {
        Street = street;
        City = city;
        Province = province;
        PostalCode = postalCode;
        Country = country;
    }

    public static Result<Address> Create(string street, string city, string province, string postalCode, string country)
    {
        if (string.IsNullOrWhiteSpace(street))
            return Result.Failure<Address>(Error.Validation("Address.Street", "Street is required."));
        if (string.IsNullOrWhiteSpace(city))
            return Result.Failure<Address>(Error.Validation("Address.City", "City is required."));
        if (string.IsNullOrWhiteSpace(province))
            return Result.Failure<Address>(Error.Validation("Address.Province", "Province is required."));

        return Result.Success(new Address(
            street.Trim(), city.Trim(), province.Trim(),
            postalCode.Trim(), string.IsNullOrWhiteSpace(country) ? "VN" : country.Trim()));
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return Street;
        yield return City;
        yield return Province;
        yield return PostalCode;
        yield return Country;
    }
}
