using NTS.Domain.Aggregates;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// How a term finds a country: by its name, its ISO code or its NF code, in any case. The profile screen finds the
/// country of a person with it, and so does the placing of an existing person in a Tenant.
/// </summary>
public sealed class CountryMatchingTests
{
    static readonly Country COUNTRY = new(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");

    [Theory]
    [InlineData("Bulgaria")]
    [InlineData("bulgaria")]
    [InlineData("BG")]
    [InlineData("bg")]
    [InlineData("BUL")]
    [InlineData("Bul")]
    public void A_term_that_is_the_name_the_ISO_code_or_the_NF_code_matches_in_any_case(string term)
    {
        Assert.True(COUNTRY.Matches(term));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Bulgari")]
    [InlineData(" Bulgaria")]
    [InlineData("BGR")]
    public void Nothing_else_matches_and_a_term_is_not_trimmed(string? term)
    {
        Assert.False(COUNTRY.Matches(term));
    }

    [Fact]
    public void A_country_without_an_NF_code_matches_by_name_and_ISO_code_only()
    {
        var country = new Country(TestId.Of(2), "Turkey", "TR", null, null);

        Assert.True(country.Matches("tr"));
        Assert.True(country.Matches("TURKEY"));
        Assert.False(country.Matches("TUR"));
    }
}
