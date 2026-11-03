using NTS.Application.Setup;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Application.Tests.Setup;

public sealed class UserSearchPolicyTests
{
    [Theory]
    [InlineData("ana@example.test")]
    [InlineData("Ana")]
    [InlineData("Marinova")]
    [InlineData("Display")]
    public void IsMatch_WhenTermMatchesIdentityField_ShouldReturnTrue(string term)
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
    public void IsMatch_WhenTermMatchesFallbackName_ShouldReturnTrue()
    {
        var user = new User("ana@example.test", "Original Name", id: 10);

        Assert.True(UserSearchPolicy.IsMatch(user, "Original"));
    }

    [Fact]
    public void IsMatch_WhenTermDoesNotMatch_ShouldReturnFalse()
    {
        var user = new User("ana@example.test", "Original Name", id: 10);

        Assert.False(UserSearchPolicy.IsMatch(user, "not-found"));
    }
}
