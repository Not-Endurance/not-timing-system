using Not.Files;

namespace NTS.Tests.Unit.Temporary;

public class NPrintRequestImageEncoderTests
{
    [Fact]
    public void CreateDataUrl_ReturnsFileDataUrl()
    {
        var imagePath = Path.Combine(AppContext.BaseDirectory, "print-encoder", "images", "logo.png");
        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
        File.WriteAllBytes(imagePath, [1, 2, 3]);

        var dataUrl = FileHelper.EncodeAsBase64DataUrl(imagePath);

        Assert.Equal("data:image/png;base64,AQID", dataUrl);
    }

    [Fact]
    public void CreateDataUrl_ReturnsNullWhenFileDoesNotExist()
    {
        var imagePath = Path.Combine(AppContext.BaseDirectory, "print-encoder", "missing.png");

        var dataUrl = FileHelper.EncodeAsBase64DataUrl(imagePath);

        Assert.Null(dataUrl);
    }
}
