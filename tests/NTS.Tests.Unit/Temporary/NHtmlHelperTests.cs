using Not.Blazor.Helpers;

namespace NTS.Tests.Unit.Temporary;

public class NHtmlHelperTests
{
    [Fact]
    public void ReplaceImagePaths_ReplacesImageSourcePaths()
    {
        var html = """
            <main>
                <img src="images/logo.png?version=1&amp;theme=dark">
                <a href="images/logo.png">Logo</a>
            </main>
            """;

        var replaced = NHtmlHelper.ReplaceImagePaths(
            html,
            imagePath =>
            {
                Assert.Equal("images/logo.png?version=1&theme=dark", imagePath);
                return "data:image/png;base64,AQID";
            }
        );

        Assert.Contains("src=\"data:image/png;base64,AQID\"", replaced);
        Assert.Contains("href=\"images/logo.png\"", replaced);
    }

    [Fact]
    public void ReplaceImagePaths_KeepsOriginalWhenReplacementIsNull()
    {
        var html = """<img src="https://example.com/logo.png">""";

        var replaced = NHtmlHelper.ReplaceImagePaths(html, _ => null);

        Assert.Equal(html, replaced);
    }

    [Fact]
    public void ReplaceImagePath_ReplacesSingleDecodedImagePath()
    {
        var replaced = NHtmlHelper.ReplaceImagePath(
            "images/logo.png?version=1&amp;theme=dark",
            imagePath =>
            {
                Assert.Equal("images/logo.png?version=1&theme=dark", imagePath);
                return "data:image/png;base64,AQID";
            }
        );

        Assert.Equal("data:image/png;base64,AQID", replaced);
    }
}
