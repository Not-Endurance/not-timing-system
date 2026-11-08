using System.Reflection;

namespace NC.Localization;

/// <summary>
/// Lists the cultures a resource file is packaged in, from the names of its embedded <c>.resources</c> files. The default
/// culture is always listed first, because the neutral file is what a missing translation falls back to.
/// </summary>
public static class ResourceCultures
{
    const string Suffix = ".resources";

    public static IReadOnlyList<string> Discover(Assembly assembly, string baseName, string defaultCulture)
    {
        return Discover(assembly.GetManifestResourceNames(), baseName, defaultCulture);
    }

    public static IReadOnlyList<string> Discover(
        IEnumerable<string> manifestResourceNames,
        string baseName,
        string defaultCulture
    )
    {
        var prefix = baseName + ".";
        var packaged = manifestResourceNames
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .Where(name => name.EndsWith(Suffix, StringComparison.Ordinal))
            .Where(name => name.Length > prefix.Length + Suffix.Length)
            .Select(name => name.Substring(prefix.Length, name.Length - prefix.Length - Suffix.Length))
            .Where(culture => !culture.Contains('.') && culture != defaultCulture)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        return [defaultCulture, .. packaged];
    }
}
