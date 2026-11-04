using NC.Server.UiClient;
using Xunit;

namespace NC.Tests.UiClient;

public sealed class BaseHrefTests
{
    [Fact]
    public void Rewrite_WhenBaseIsRoot_ShouldPointBaseAtPrefix()
    {
        var html = "<head><base href=\"/\" /></head>";

        Assert.Equal("<head><base href=\"/ui/\" /></head>", BaseHref.Rewrite(html, "/ui"));
    }

    [Fact]
    public void Rewrite_WhenBaseUsesSingleQuotes_ShouldPointBaseAtPrefix()
    {
        var html = "<head><base href='/' /></head>";

        Assert.Equal("<head><base href=\"/ui/\" /></head>", BaseHref.Rewrite(html, "/ui"));
    }

    [Fact]
    public void Rewrite_WhenPageHasNoBaseElement_ShouldReturnItUnchanged()
    {
        var html = "<head></head>";

        Assert.Equal(html, BaseHref.Rewrite(html, "/ui"));
    }
}
