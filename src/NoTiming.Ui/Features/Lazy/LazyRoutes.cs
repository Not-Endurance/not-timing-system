namespace NoTiming.Ui.Features.Lazy;

/// <summary>
/// Which paths of the app belong to an assembly that is fetched when a person goes there and not when the app opens
/// (#645). A viewer downloads the app to read a startlist: the pages of the Console, which only an Operator uses, are not
/// part of that download. The Console (#646) fills this in: it names the first segment of its paths and its files here, and
/// lists the files in the project of the app as <c>BlazorWebAssemblyLazyLoad</c> items; the router fetches them before it
/// looks for a page, and nothing else here changes. Until then nothing is lazy.
/// </summary>
public static class LazyRoutes
{
    /// <summary>The files the path needs: those of the prefix it is under, none when it is under none.</summary>
    public static IReadOnlyList<string> NeededBy(string path, IReadOnlyDictionary<string, string[]> assemblies)
    {
        var first = FirstSegment(path);
        var named = assemblies.FirstOrDefault(x => string.Equals(x.Key, first, StringComparison.OrdinalIgnoreCase));
        return first != null ? named.Value ?? [] : [];
    }

    /// <summary>
    /// The files a path needs, by the first segment of the path, such as <c>console</c> for <c>NoTiming.Console.wasm</c>. A
    /// segment is a whole segment (<c>consoles</c> is not <c>console</c>), whatever its case.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> Assemblies { get; } =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

    static string? FirstSegment(string path)
    {
        var segments = (path ?? string.Empty).Split('?', '#')[0].Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0 ? null : segments[0];
    }
}
