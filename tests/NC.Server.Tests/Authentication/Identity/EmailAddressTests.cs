using NC.Server.Authentication.Identity;
using Xunit;

namespace NC.Server.Tests.Authentication.Identity;

public sealed class EmailAddressTests
{
    [Fact]
    public void Normalize_WhenMixedCase_ShouldLowerCase()
    {
        Assert.Equal("ana.ivanova@example.test", EmailAddress.Normalize("Ana.Ivanova@Example.TEST"));
    }

    [Fact]
    public void Normalize_WhenSurroundedBySpaces_ShouldTrim()
    {
        Assert.Equal("ana@example.test", EmailAddress.Normalize("  ana@example.test \t"));
    }
}
