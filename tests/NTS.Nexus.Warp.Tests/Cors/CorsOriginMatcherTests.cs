using NTS.Nexus.Warp.Cors;

namespace NTS.Nexus.Warp.Tests.Cors;

public sealed class CorsOriginMatcherTests
{
    [Fact]
    public void IsSameHost_WhenOriginHostMatchesRequestHost_ShouldReturnTrue()
    {
        var result = CorsOriginMatcher.IsSameHost("https://nts.example.com", "nts.example.com");

        Assert.True(result);
    }

    [Fact]
    public void IsSameHost_WhenHostDiffersOnlyInCase_ShouldReturnTrue()
    {
        var result = CorsOriginMatcher.IsSameHost("https://NTS.example.com", "nts.example.com");

        Assert.True(result);
    }

    [Fact]
    public void IsSameHost_WhenOriginSchemeIsHttp_ShouldReturnTrue()
    {
        // App Service terminates TLS, so the scheme Warp sees can differ from the browser's origin.
        var result = CorsOriginMatcher.IsSameHost("http://nts.example.com", "nts.example.com");

        Assert.True(result);
    }

    [Fact]
    public void IsSameHost_WhenOriginIsIpv6Literal_ShouldReturnTrue()
    {
        var result = CorsOriginMatcher.IsSameHost("http://[::1]:5000", "::1");

        Assert.True(result);
    }

    [Fact]
    public void IsSameHost_WhenOriginHostDiffers_ShouldReturnFalse()
    {
        var result = CorsOriginMatcher.IsSameHost("https://other.example.com", "nts.example.com");

        Assert.False(result);
    }

    [Fact]
    public void IsSameHost_WhenOriginHostOnlyStartsWithRequestHost_ShouldReturnFalse()
    {
        var result = CorsOriginMatcher.IsSameHost("https://nts.example.com.evil.net", "nts.example.com");

        Assert.False(result);
    }

    [Fact]
    public void IsSameHost_WhenOriginIsNotAbsolute_ShouldReturnFalse()
    {
        var result = CorsOriginMatcher.IsSameHost("nts.example.com", "nts.example.com");

        Assert.False(result);
    }

    [Fact]
    public void IsSameHost_WhenOriginIsNull_ShouldReturnFalse()
    {
        var result = CorsOriginMatcher.IsSameHost(null, "nts.example.com");

        Assert.False(result);
    }
}
