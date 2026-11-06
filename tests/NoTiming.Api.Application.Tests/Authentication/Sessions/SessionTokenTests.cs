using NoTiming.Api.Application.Authentication.Sessions;

namespace NoTiming.Api.Application.Tests.Authentication.Sessions;

public sealed class SessionTokenTests
{
    [Fact]
    public void Issue_WhenCalled_ShouldReturnA256BitToken()
    {
        var issued = SessionToken.Issue();

        Assert.Matches("^[0-9a-f]{64}$", issued.Token);
    }

    [Fact]
    public void Issue_WhenCalledTwice_ShouldReturnDifferentTokens()
    {
        var first = SessionToken.Issue();
        var second = SessionToken.Issue();

        Assert.NotEqual(first.Token, second.Token);
    }

    [Fact]
    public void Issue_WhenCalled_ShouldStoreTheHashOfTheToken()
    {
        var issued = SessionToken.Issue();

        Assert.Equal(SessionToken.HashOf(issued.Token), issued.TokenHash);
    }

    [Fact]
    public void HashOf_WhenGivenAbc_ShouldReturnTheSha256Digest()
    {
        // SHA-256("abc"), from FIPS 180-2 appendix B.1.
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", SessionToken.HashOf("abc"));
    }
}
