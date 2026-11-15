using NC.Localization;
using Xunit;

namespace NC.Tests.Localization;

public sealed class ResourceCulturesTests
{
    const string BaseName = "NoTiming.Shared.Localization.Resources.LocalizedStrings";

    [Fact]
    public void Discover_WhenCulturesArePackaged_ShouldListTheDefaultThenEachCulture()
    {
        var names = new[]
        {
            "NoTiming.Shared.Localization.Resources.LocalizedStrings.tr.resources",
            "NoTiming.Shared.Localization.Resources.LocalizedStrings.resources",
            "NoTiming.Shared.Localization.Resources.LocalizedStrings.bg.resources",
        };

        var cultures = ResourceCultures.Discover(names, BaseName, "en");

        Assert.Equal(new[] { "en", "bg", "tr" }, cultures);
    }

    [Fact]
    public void Discover_WhenNoCultureIsPackaged_ShouldListOnlyTheDefault()
    {
        var names = new[] { "NoTiming.Shared.Localization.Resources.LocalizedStrings.resources" };

        var cultures = ResourceCultures.Discover(names, BaseName, "en");

        Assert.Equal(new[] { "en" }, cultures);
    }

    [Fact]
    public void Discover_WhenDefaultIsNotPackaged_ShouldStillListTheDefault()
    {
        var names = new[] { "NoTiming.Shared.Localization.Resources.LocalizedStrings.bg.resources" };

        var cultures = ResourceCultures.Discover(names, BaseName, "en");

        Assert.Equal(new[] { "en", "bg" }, cultures);
    }

    [Fact]
    public void Discover_WhenNameHasMoreThanOneCultureSegment_ShouldIgnoreIt()
    {
        var names = new[] { "NoTiming.Shared.Localization.Resources.LocalizedStrings.Extra.bg.resources" };

        var cultures = ResourceCultures.Discover(names, BaseName, "en");

        Assert.Equal(new[] { "en" }, cultures);
    }

    [Fact]
    public void Discover_WhenAnotherResourceSharesTheStart_ShouldIgnoreIt()
    {
        var names = new[] { "NoTiming.Shared.Localization.Resources.LocalizedStringsHelper.bg.resources" };

        var cultures = ResourceCultures.Discover(names, BaseName, "en");

        Assert.Equal(new[] { "en" }, cultures);
    }
}
