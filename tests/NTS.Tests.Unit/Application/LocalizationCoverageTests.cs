using System.Globalization;
using System.Reflection;
using System.Resources;
using Not.Localization;
using NTS.Localization;
using NTS.Localization.Resources;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// Text is available in English, Bulgarian and Turkish (#645): every string that a page or a component of the app uses has
/// a text in each of the three, so that a person who chose a language never sees the name of a string, or an empty place
/// where a label should be. A string that no source uses is not asked for (it waits for a page that brings it back).
/// </summary>
public sealed class LocalizationCoverageTests
{
    static readonly string[] LANGUAGES = ["en", "bg", "tr"];

    [Theory]
    [InlineData("en")]
    [InlineData("bg")]
    [InlineData("tr")]
    public void Every_string_the_app_uses_has_a_text_in_the_language(string language)
    {
        var resources = new ResourceManager(typeof(LocalizedStrings));
        var culture = language == "en" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(language);
        var set = resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        Assert.NotNull(set);

        var lacking = UsedKeys().Where(key => string.IsNullOrWhiteSpace(set!.GetString(key))).Order().ToArray();

        Assert.True(lacking.Length == 0, $"No text in '{language}' for: {string.Join(", ", lacking)}");
    }

    [Fact]
    public void The_languages_asked_for_are_the_languages_the_person_can_choose()
    {
        Assert.Equal(LANGUAGES, NoTiming.Ui.Features.Account.Languages.Supported.Select(x => x.Code));
    }

    /// <summary>The accessors of the strings that a source other than the accessors names.</summary>
    static IEnumerable<string> UsedKeys()
    {
        var root = RepositoryRoot();
        var sources = new[] { "src", "nugets" }
            .SelectMany(folder =>
                Directory.EnumerateFiles(Path.Combine(root, folder), "*.*", SearchOption.AllDirectories).Where(IsSource)
            )
            .Select(file => File.ReadAllText(file))
            .ToArray();

        return AccessorKeys().Where(key => sources.Any(text => text.Contains(key, StringComparison.Ordinal)));
    }

    /// <summary>The names of the strings of the two accessors, which are their keys.</summary>
    static IEnumerable<string> AccessorKeys()
    {
        return new[] { typeof(NtsStrings), typeof(NStrings) }
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Static))
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => property.Name)
            .Where(name => name is not ("hash_string" or "X_string")) // these two name no key: they are the text itself
            .Distinct();
    }

    static bool IsSource(string file)
    {
        var path = file.Replace('\\', '/');
        if (path.Contains("/bin/") || path.Contains("/obj/") || path.Contains("/wwwroot/"))
        {
            return false;
        }

        return (path.EndsWith(".cs") || path.EndsWith(".razor"))
            && !path.EndsWith("/Localization/NtsStrings.cs")
            && !path.EndsWith("/Localization/NStrings.cs");
    }

    static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "src", "not-timing-system.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new InvalidOperationException("The repository was not found above the tests.");
    }
}
