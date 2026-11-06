using NC.Domain.Exceptions;
using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Core.Objects;

namespace NoTiming.Domain.Core.Tests.Objects;

public sealed class EventTimeZoneTests
{
    [Fact]
    public void ForCountry_WhenCountryIsBulgaria_ShouldReturnSofia()
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");

        Assert.Equal("Europe/Sofia", EventTimeZone.ForCountry(country));
    }

    [Fact]
    public void ForCountry_WhenCountryIsTurkey_ShouldReturnIstanbul()
    {
        var country = new Country(2, "Turkey", "TR", "TUR", "tr-TR");

        Assert.Equal("Europe/Istanbul", EventTimeZone.ForCountry(country));
    }

    [Fact]
    public void ForCountry_WhenBulgariaHasThreeLetterCode_ShouldReturnSofia()
    {
        var country = new Country(37, "Bulgaria", "BGR", "BUL", "bg-BG");

        Assert.Equal("Europe/Sofia", EventTimeZone.ForCountry(country));
    }

    [Fact]
    public void ForCountry_WhenTurkeyHasThreeLetterCode_ShouldReturnIstanbul()
    {
        var country = new Country(38, "Turkey", "TUR", "TUR", "tr-TR");

        Assert.Equal("Europe/Istanbul", EventTimeZone.ForCountry(country));
    }

    [Fact]
    public void ForCountry_WhenIsoCodeIsLowerCase_ShouldReturnSofia()
    {
        var country = new Country(37, "Bulgaria", "bg", "BUL", "bg-BG");

        Assert.Equal("Europe/Sofia", EventTimeZone.ForCountry(country));
    }

    [Fact]
    public void ForCountry_WhenCountryIsUnknown_ShouldThrow()
    {
        var country = new Country(3, "Nowhere", "XX", null, null);

        Assert.Throws<DomainException>(() => EventTimeZone.ForCountry(country));
    }
}
