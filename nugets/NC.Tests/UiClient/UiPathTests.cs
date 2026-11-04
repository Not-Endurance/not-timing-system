using Not.Server.UiClient;
using Xunit;

namespace Not.Tests.UiClient;

public sealed class UiPathTests
{
    [Fact]
    public void Normalize_WhenPrefixHasNoSlashes_ShouldAddLeadingSlash()
    {
        Assert.Equal("/ui", UiPath.Normalize("ui"));
    }

    [Fact]
    public void Normalize_WhenPrefixHasTrailingSlash_ShouldRemoveIt()
    {
        Assert.Equal("/ui", UiPath.Normalize("/ui/"));
    }

    [Fact]
    public void Normalize_WhenPrefixIsBlank_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => UiPath.Normalize(" "));
    }
}
