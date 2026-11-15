using NC.Server.Authentication.Profiles;
using NC.Server.Tests.Authentication.Fakes;
using Xunit;

namespace NC.Server.Tests.Authentication.Profiles;

public sealed class ProfileRulesTests
{
    [Fact]
    public void Accepts_WhenNoValidatorIsRegistered_ShouldAcceptTheProfile()
    {
        var rules = new ProfileRules<TestProfile>([]);

        Assert.True(rules.Accepts(new TestProfile()));
    }

    [Fact]
    public void Accepts_WhenOneValidatorRejects_ShouldReturnFalse()
    {
        var rules = new ProfileRules<TestProfile>(
            [
                new FakeProfileValidator(profile => true),
                new FakeProfileValidator(profile => profile.CountryIsoCode is not null),
            ]
        );

        Assert.False(rules.Accepts(new TestProfile()));
    }

    [Fact]
    public void Accepts_WhenEveryValidatorAccepts_ShouldReturnTrue()
    {
        var rules = new ProfileRules<TestProfile>(
            [new FakeProfileValidator(profile => true), new FakeProfileValidator(profile => profile.Club is not null)]
        );

        Assert.True(rules.Accepts(new TestProfile { Club = "SC Ruse" }));
    }
}
