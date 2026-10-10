using NTS.Contracts.Features.Account;
using NTS.Contracts.Features.Profile;
using NTS.Domain.Aggregates;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// What the Ui asks of a profile and what it calls the person (#645, ADR-0001). A profile that is to be saved names a first
/// name, a surname and a country, which sending a Snapshot asks for and nothing else does; the drawer calls the person by
/// the first name of the profile, or the name of the account, or the address, cut to what fits, and a visitor by none.
/// </summary>
public sealed class WitnessProfilePolicyTests
{
    static readonly Country BULGARIA = new(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");

    [Fact]
    public void A_profile_with_a_first_name_a_surname_and_a_country_is_complete()
    {
        Assert.True(WitnessProfilePolicy.IsComplete(Model("Ana", "Petrova", BULGARIA)));
    }

    [Theory]
    [InlineData(null, "Petrova", true)]
    [InlineData(" ", "Petrova", true)]
    [InlineData("Ana", null, true)]
    [InlineData("Ana", "\t", true)]
    [InlineData("Ana", "Petrova", false)]
    public void A_profile_that_lacks_a_first_name_a_surname_or_a_country_is_not_complete(
        string? givenName,
        string? surname,
        bool hasCountry
    )
    {
        var model = Model(givenName, surname, hasCountry ? BULGARIA : null);

        Assert.False(WitnessProfilePolicy.IsComplete(model));
    }

    [Fact]
    public void Nothing_is_a_profile_that_is_complete()
    {
        Assert.False(WitnessProfilePolicy.IsComplete(null));
    }

    [Fact]
    public void A_visitor_is_called_by_no_name_whatever_they_hold()
    {
        var profile = new AccountProfile { GivenName = "Ana" };

        Assert.Equal("", WitnessProfilePolicy.ResolveWelcomeName(profile, null));
        Assert.Equal("", WitnessProfilePolicy.ResolveWelcomeName(null, null));
    }

    [Fact]
    public void A_person_is_called_by_the_first_name_of_the_profile_then_the_name_of_the_account_then_the_address()
    {
        var withName = Account(name: "Ana Petrova", email: "ana@ex.test");
        var withEmail = Account(name: " ", email: "ana@ex.test");

        Assert.Equal(
            "Vera",
            WitnessProfilePolicy.ResolveWelcomeName(new AccountProfile { GivenName = "Vera" }, withName)
        );
        Assert.Equal("Ana Petrova", WitnessProfilePolicy.ResolveWelcomeName(new AccountProfile(), withName));
        Assert.Equal("Ana Petrova", WitnessProfilePolicy.ResolveWelcomeName(null, withName));
        Assert.Equal("ana@ex.test", WitnessProfilePolicy.ResolveWelcomeName(null, withEmail));
        Assert.Equal(
            "Vera",
            WitnessProfilePolicy.ResolveWelcomeName(new AccountProfile { GivenName = " Vera " }, withName)
        );
    }

    [Theory]
    [InlineData("Ana", "Ana")]
    [InlineData("Elisabetta", "Elisabetta")]
    [InlineData("Konstantin1", "Konstantin1")] // eleven characters
    [InlineData("Konstantin12", "Konstantin12")] // twelve characters
    [InlineData("Konstantin123", "Konstantin12")] // thirteen
    [InlineData("Bartholomew-Alexander", "Bartholomew-")]
    public void The_name_is_cut_to_twelve_characters_and_a_shorter_one_is_kept(string given, string expected)
    {
        Assert.Equal(
            expected,
            WitnessProfilePolicy.ResolveWelcomeName(
                new AccountProfile { GivenName = given },
                Account("A. B.", "a@b.test")
            )
        );
    }

    static WitnessProfileFormModel Model(string? givenName, string? surname, Country? country)
    {
        return new WitnessProfileFormModel
        {
            GivenName = givenName,
            Surname = surname,
            Country = country,
        };
    }

    static CurrentAccount Account(string? name, string email)
    {
        return new CurrentAccount
        {
            Id = TestId.Of(1),
            Email = email,
            Name = name,
        };
    }
}
