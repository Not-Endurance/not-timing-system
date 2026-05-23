using NTS.Domain.Setup.Aggregates;

namespace NTS.Tests.Unit.Domain;

public sealed class UserTests
{
    [Fact]
    public void ToString_includes_display_name_name_and_email()
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
    public void ToString_falls_back_to_email_when_no_distinct_name_exists()
    {
        var user = new User("rider@example.test", "rider@example.test", id: 1);

        Assert.Equal("rider@example.test", user.ToString());
    }
}
