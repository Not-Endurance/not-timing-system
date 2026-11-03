using NTS.Domain.Setup.Aggregates;

namespace NTS.Domain.Setup.Tests.Aggregates;

public sealed class UserTests
{
    [Fact]
    public void ToString_WhenDisplayNameAndNameExist_ShouldIncludeDisplayNameNameAndEmail()
    {
        var user = new User(
            "rider@example.test",
            "Fallback Rider",
            id: 1,
            givenName: "Rosa",
            surname: "Rider",
            displayName: "Rosa Display"
        );

        Assert.Equal("Rosa Display / Rosa Rider (rider@example.test)", user.ToString());
    }

    [Fact]
    public void ToString_WhenNoDistinctNameExists_ShouldFallBackToEmail()
    {
        var user = new User("rider@example.test", "rider@example.test", id: 1);

        Assert.Equal("rider@example.test", user.ToString());
    }
}
