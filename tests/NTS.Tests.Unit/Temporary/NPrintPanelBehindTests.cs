using Not.Blazor.Components.Print;
using Not.Print;

namespace NTS.Tests.Unit.Temporary;

public class NPrintPanelBehindTests
{
    [Fact]
    public void EncodeStaticImageSources_ReplacesLocalImageSourcesWithDataUrls()
    {
        var webRootPath = Path.Combine(AppContext.BaseDirectory, "print-webroot");
        var imagePath = Path.Combine(webRootPath, "images", "logo.png");
        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
        File.WriteAllBytes(imagePath, [1, 2, 3]);

        var request = new NPrintDocumentRequest
        {
            Html = """
                <main>
                    <img src="images/logo.png">
                    <img src="https://example.com/logo.png">
                </main>
                """,
        };

        var encoded = NPrintPanelBehind.EncodeStaticImageSources(request, [webRootPath]);

        Assert.Contains("src=\"data:image/png;base64,AQID\"", encoded.Html);
        Assert.Contains("src=\"https://example.com/logo.png\"", encoded.Html);
    }

    [Fact]
    public void EncodeStaticImageSources_ResolvesImagesFromMultipleRoots()
    {
        var webRootPath = Path.Combine(AppContext.BaseDirectory, "print-webroot-primary");
        var appRootPath = Path.Combine(AppContext.BaseDirectory, "print-app-root");
        var headerLogoPath = Path.Combine(webRootPath, "images", "logo.png");
        var appIconPath = Path.Combine(appRootPath, "Resources", "AppIcon", "appicon.svg");
        Directory.CreateDirectory(Path.GetDirectoryName(headerLogoPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(appIconPath)!);
        File.WriteAllBytes(headerLogoPath, [1, 2, 3]);
        File.WriteAllText(appIconPath, "<svg />");

        var request = new NPrintDocumentRequest
        {
            Html = """
                <main>
                    <img src="/images/logo.png">
                    <img src="Resources/AppIcon/appicon.svg">
                </main>
                """,
        };

        var encoded = NPrintPanelBehind.EncodeStaticImageSources(request, [webRootPath, appRootPath]);

        Assert.Contains("src=\"data:image/png;base64,AQID\"", encoded.Html);
        Assert.Contains("src=\"data:image/svg+xml;base64,PHN2ZyAvPg==\"", encoded.Html);
    }
}
