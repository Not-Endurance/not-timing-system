using Not.Server.UiClient;
using Xunit;

namespace Not.Tests.UiClient;

public sealed class PrecompressedEncodingTests
{
    [Fact]
    public void Select_WhenBrotliAndGzipAccepted_ShouldChooseBrotli()
    {
        var variant = PrecompressedEncoding.Select("gzip, br", _ => true);

        Assert.Equal("br", variant?.Encoding);
    }

    [Fact]
    public void Select_WhenOnlyGzipAccepted_ShouldChooseGzip()
    {
        var variant = PrecompressedEncoding.Select("gzip", _ => true);

        Assert.Equal("gzip", variant?.Encoding);
    }

    [Fact]
    public void Select_WhenBrotliVariantIsMissing_ShouldChooseGzip()
    {
        var variant = PrecompressedEncoding.Select("br, gzip", extension => extension == ".gz");

        Assert.Equal("gzip", variant?.Encoding);
    }

    [Fact]
    public void Select_WhenBrotliIsRefusedWithZeroQuality_ShouldChooseGzip()
    {
        var variant = PrecompressedEncoding.Select("br;q=0, gzip", _ => true);

        Assert.Equal("gzip", variant?.Encoding);
    }

    [Fact]
    public void Select_WhenNoPrecompressedEncodingIsAccepted_ShouldReturnNull()
    {
        var variant = PrecompressedEncoding.Select("identity", _ => true);

        Assert.Null(variant);
    }

    [Fact]
    public void Select_WhenAcceptEncodingIsMissing_ShouldReturnNull()
    {
        var variant = PrecompressedEncoding.Select(null, _ => true);

        Assert.Null(variant);
    }
}
