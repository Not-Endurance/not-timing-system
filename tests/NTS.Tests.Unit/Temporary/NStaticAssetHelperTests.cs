using Not.Blazor.Helpers;

namespace NTS.Tests.Unit.Temporary;

public class NStaticAssetHelperTests
{
    [Fact]
    public void CreateRootPaths_ReturnsHostAndWebRootPaths()
    {
        var rootPaths = NStaticAssetHelper.CreateRootPaths();

        Assert.Contains(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "wwwroot")), rootPaths);
        Assert.Contains(Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "wwwroot")), rootPaths);
        Assert.Contains(Path.GetFullPath(AppContext.BaseDirectory), rootPaths);
        Assert.Contains(Path.GetFullPath(Environment.CurrentDirectory), rootPaths);
        Assert.Equal(rootPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count(), rootPaths.Count);
    }

    [Fact]
    public void ResolvePath_ResolvesRelativePathsFromRootPaths()
    {
        var rootPath = Path.Combine(AppContext.BaseDirectory, "asset-roots");
        var filePath = Path.Combine(rootPath, "images", "logo.png");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllBytes(filePath, [1, 2, 3]);

        var resolvedPath = NStaticAssetHelper.ResolvePath("/images/logo.png?version=1", [rootPath]);

        Assert.Equal(filePath, resolvedPath);
    }

    [Fact]
    public void ResolvePath_SkipsRemoteAndEncodedPaths()
    {
        Assert.Null(NStaticAssetHelper.ResolvePath("https://example.com/logo.png", []));
        Assert.Null(NStaticAssetHelper.ResolvePath("data:image/png;base64,AQID", []));
    }
}
