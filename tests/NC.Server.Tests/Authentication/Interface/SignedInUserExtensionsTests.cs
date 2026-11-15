using System.Security.Claims;
using NC.Server.Authentication.Interface;
using Xunit;

namespace NC.Server.Tests.Authentication.Interface;

public sealed class SignedInUserExtensionsTests
{
    [Fact]
    public void GetSignedInUserId_WhenTheIdIsAnInteger_ShouldReturnIt()
    {
        var principal = Principal(new Claim(ClaimTypes.NameIdentifier, "42"));

        Assert.Equal((int?)42, principal.GetSignedInUserId());
    }

    [Fact]
    public void GetSignedInUserId_WhenTheIdIsNotAnInteger_ShouldBeNull()
    {
        var principal = Principal(new Claim(ClaimTypes.NameIdentifier, "not-a-number"));

        Assert.Null(principal.GetSignedInUserId());
    }

    [Fact]
    public void GetRequiredSignedInUserId_WhenTheIdIsAnInteger_ShouldReturnIt()
    {
        var principal = Principal(new Claim(ClaimTypes.NameIdentifier, "7"));

        Assert.Equal(7, principal.GetRequiredSignedInUserId());
    }

    [Fact]
    public void GetRequiredSignedInUserId_WhenTheIdIsMissing_ShouldThrow()
    {
        var principal = Principal();

        Assert.Throws<InvalidOperationException>(() => principal.GetRequiredSignedInUserId());
    }

    static ClaimsPrincipal Principal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}
