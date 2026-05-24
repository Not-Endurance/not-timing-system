using NTS.Application.Setup;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Tests.Unit.Application;

public sealed class UserSearchPolicyTests
{
    [Theory]
    [InlineData("ana@example.test")]
    [InlineData("Ana")]
    [InlineData("Marinova")]
    [InlineData("Display")]
    public void IsMatch_searches_user_identity_fields(string term)
    {
        var user = new User(
            "ana@example.test",
            "Original Name",
            id: 10,
            givenName: "Ana",
            surname: "Marinova",
            displayName: "Ana Display"
        );

        Assert.True(UserSearchPolicy.IsMatch(user, term));
    }

    [Fact]
    public void IsMatch_searches_fallback_name()
    {
        var user = new User("ana@example.test", "Original Name", id: 10);

        Assert.True(UserSearchPolicy.IsMatch(user, "Original"));
    }

    [Fact]
    public void IsMatch_rejects_unmatched_term()
    {
        var user = new User("ana@example.test", "Original Name", id: 10);

        Assert.False(UserSearchPolicy.IsMatch(user, "not-found"));
    }
}
