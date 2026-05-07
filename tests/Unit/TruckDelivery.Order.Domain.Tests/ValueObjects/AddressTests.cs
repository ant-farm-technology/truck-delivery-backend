using Xunit;
using FluentAssertions;
using TruckDelivery.Order.Domain.ValueObjects;

namespace TruckDelivery.Order.Domain.Tests.ValueObjects;

public sealed class AddressTests
{
    [Fact]
    public void Create_Should_Succeed_WithValidFields()
    {
        var result = Address.Create("123 Main St", "Ho Chi Minh", "Ho Chi Minh", "70000", "VN");

        result.IsSuccess.Should().BeTrue();
        result.Value.Street.Should().Be("123 Main St");
        result.Value.City.Should().Be("Ho Chi Minh");
        result.Value.Country.Should().Be("VN");
    }

    [Theory]
    [InlineData("", "City", "Province")]
    [InlineData("   ", "City", "Province")]
    public void Create_Should_Fail_WhenStreetEmpty(string street, string city, string province)
    {
        var result = Address.Create(street, city, province, "", "VN");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Address.Street");
    }

    [Theory]
    [InlineData("Street", "", "Province")]
    [InlineData("Street", "   ", "Province")]
    public void Create_Should_Fail_WhenCityEmpty(string street, string city, string province)
    {
        var result = Address.Create(street, city, province, "", "VN");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Address.City");
    }

    [Fact]
    public void Create_Should_DefaultCountry_WhenEmpty()
    {
        var result = Address.Create("St", "City", "Prov", "100", "");

        result.IsSuccess.Should().BeTrue();
        result.Value.Country.Should().Be("VN");
    }

    [Fact]
    public void TwoAddresses_WithSameValues_ShouldBeEqual()
    {
        var a1 = Address.Create("St", "City", "Prov", "100", "VN").Value;
        var a2 = Address.Create("St", "City", "Prov", "100", "VN").Value;

        a1.Should().Be(a2);
    }

    [Fact]
    public void TwoAddresses_WithDifferentCity_ShouldNotBeEqual()
    {
        var a1 = Address.Create("St", "HCM", "Prov", "100", "VN").Value;
        var a2 = Address.Create("St", "HN", "Prov", "100", "VN").Value;

        a1.Should().NotBe(a2);
    }
}
