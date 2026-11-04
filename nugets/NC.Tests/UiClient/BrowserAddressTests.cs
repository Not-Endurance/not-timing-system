using Not.Server.UiClient;
using Xunit;

namespace Not.Tests.UiClient;

public sealed class BrowserAddressTests
{
    [Fact]
    public void From_WhenAddressUsesIpv6Wildcard_ShouldUseLocalhost()
    {
        var url = BrowserAddress.From(["http://[::]:11337"], "/");

        Assert.Equal("http://localhost:11337/", url);
    }

    [Fact]
    public void From_WhenAddressUsesIpv4Wildcard_ShouldUseLocalhost()
    {
        var url = BrowserAddress.From(["http://0.0.0.0:5000"], "/");

        Assert.Equal("http://localhost:5000/", url);
    }

    [Fact]
    public void From_WhenAddressNamesAHost_ShouldKeepHostAndScheme()
    {
        var url = BrowserAddress.From(["https://localhost:5001"], "/");

        Assert.Equal("https://localhost:5001/", url);
    }

    [Fact]
    public void From_WhenThereAreNoAddresses_ShouldReturnNull()
    {
        var url = BrowserAddress.From([], "/");

        Assert.Null(url);
    }

    [Fact]
    public void From_WhenPathIsGiven_ShouldAppendItToTheUrl()
    {
        var url = BrowserAddress.From(["http://[::]:11337"], "/ui/");

        Assert.Equal("http://localhost:11337/ui/", url);
    }
}
