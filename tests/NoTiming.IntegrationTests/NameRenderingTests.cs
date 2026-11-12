using System.Globalization;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Helpers;

namespace NoTiming.IntegrationTests;

public sealed class NameRenderingTests
{
    [Fact]
    public void Render_UsesEnglishForUnspecifiedOrEnglishCulture()
    {
        Assert.Equal(
            "English Name",
            NameRenderingHelper.Render("Local Name", "English Name", culture: CultureInfo.InvariantCulture)
        );
        Assert.Equal(
            "English Name",
            NameRenderingHelper.Render("Local Name", "English Name", culture: new CultureInfo("en-US"))
        );
    }

    [Fact]
    public void Render_UsesLocalNameForNonEnglishCulture()
    {
        Assert.Equal(
            "Local Name",
            NameRenderingHelper.Render("Local Name", "English Name", culture: new CultureInfo("bg-BG"))
        );
    }

    [Fact]
    public void Render_UsesEnglishForFeiRulesetAndFallsBackToNameWhenBlank()
    {
        Assert.Equal(
            "English Name",
            NameRenderingHelper.Render("Local Name", "English Name", true, new CultureInfo("bg-BG"))
        );
        Assert.Equal("Local Name", NameRenderingHelper.Render("Local Name", null, true, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Render_FallsBackToEnglishWhenNameIsBlank()
    {
        Assert.Equal("English Name", NameRenderingHelper.Render("", "English Name", culture: new CultureInfo("bg-BG")));
    }
}
