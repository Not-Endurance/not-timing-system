namespace Not.Blazor.Helpers;

internal static class NStaticAssetHelper
{
    public static IReadOnlyList<string> CreateRootPaths()
    {
        return new[]
        {
            Path.Combine(AppContext.BaseDirectory, "wwwroot"),
            Path.Combine(Environment.CurrentDirectory, "wwwroot"),
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
        }
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string? ResolvePath(string filePath, IReadOnlyList<string> rootPaths)
    {
        if (string.IsNullOrWhiteSpace(filePath) || IsRemoteOrEncoded(filePath))
        {
            return null;
        }

        if (Uri.TryCreate(filePath, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeFile)
        {
            return Path.GetFullPath(uri.LocalPath);
        }

        var localPath = RemoveQueryAndFragment(filePath);
        if (Path.IsPathFullyQualified(localPath))
        {
            return Path.GetFullPath(localPath);
        }

        var relativePath = localPath.TrimStart('/', '\\');
        foreach (var rootPath in rootPaths)
        {
            var fullPath = Path.Combine(rootPath, relativePath);
            if (File.Exists(fullPath))
            {
                return Path.GetFullPath(fullPath);
            }
        }

        return null;
    }

    static bool IsRemoteOrEncoded(string filePath)
    {
        if (filePath.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Uri.TryCreate(filePath, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    static string RemoveQueryAndFragment(string filePath)
    {
        var suffixIndex = filePath.IndexOfAny(['?', '#']);
        return suffixIndex < 0 ? filePath : filePath[..suffixIndex];
    }
}
