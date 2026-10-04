using NTS.Domain.Aggregates;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// A Tenant is a country's equestrian federation, created on demand from a country that has an ISO code (ADR-0002,
/// ADR-0012). A person is placed in the Tenant of their country when they register, and an existing person is placed
/// by the country of their profile with the matching the profile screen uses.
/// </summary>
public sealed class TenantTests
{
    static readonly Country BULGARIA = new(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
    static readonly Country TURKEY = new(TestId.Of(2), "Turkey", "TR", "TUR", "tr-TR");
    static readonly Country[] COUNTRIES = [BULGARIA, TURKEY];

    [Fact]
    public void A_country_gives_a_Tenant_with_its_ISO_code_in_the_id_its_name_and_the_kind_country()
    {
        var tenant = Tenant.ForCountry(BULGARIA);

        Assert.Equal("country-bg", tenant.Id);
        Assert.Equal("Bulgaria", tenant.Name);
        Assert.Equal("country", tenant.Kind);
    }

    [Fact]
    public void The_id_follows_the_ISO_code_as_it_is_written_whatever_its_case_or_length()
    {
        var tenant = Tenant.ForCountry(new Country(TestId.Of(3), "Bulgaria", "BGR", "BGN", null));

        Assert.Equal("country-bgr", tenant.Id);
    }

    [Theory]
    [InlineData("Bulgaria")]
    [InlineData("BULGARIA")]
    [InlineData("bg")]
    [InlineData("BUL")]
    [InlineData("bul")]
    public void An_existing_person_is_placed_by_the_name_the_ISO_code_or_the_NF_code_of_the_country_of_their_profile(
        string profileCountry
    )
    {
        var tenant = Tenant.DerivedFrom(profileCountry, COUNTRIES);

        Assert.Equal("country-bg", tenant?.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Atlantis")]
    [InlineData("Bulgari")]
    public void A_person_whose_profile_country_is_missing_or_matches_no_country_is_placed_nowhere(
        string? profileCountry
    )
    {
        Assert.Null(Tenant.DerivedFrom(profileCountry, COUNTRIES));
    }

    [Fact]
    public void When_a_term_matches_two_countries_the_first_in_the_list_wins_as_it_does_on_the_profile_screen()
    {
        var namesake = new Country(TestId.Of(4), "Turkey", "XX", "XXX", null);

        var tenant = Tenant.DerivedFrom("Turkey", [namesake, TURKEY]);

        Assert.Equal("country-xx", tenant?.Id);
    }

    [Fact]
    public void A_Membership_names_a_Tenant_and_the_roles_held_in_it_and_a_new_one_holds_none()
    {
        var home = Membership.In(Tenant.ForCountry(BULGARIA));
        var root = new Membership("country-bg", ["TenantRoot"]);

        Assert.Equal("country-bg", home.TenantId);
        Assert.Empty(home.Roles);
        Assert.Equal(["TenantRoot"], root.Roles);
    }
}
