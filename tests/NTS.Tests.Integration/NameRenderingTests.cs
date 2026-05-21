using System.Globalization;
using NTS.Domain.Enums;
using NTS.Domain.Helpers;

namespace NTS.Tests.Integration;

public sealed class NameRenderingTests
{
    [Fact]
    public void Render_UsesEnglishForUnspecifiedOrEnglishCulture()
    {
        Assert.Equal("English Name", NameRenderingHelper.Render("Local Name", "English Name", culture: CultureInfo.InvariantCulture));
        Assert.Equal("English Name", NameRenderingHelper.Render("Local Name", "English Name", culture: new CultureInfo("en-US")));
    }

    [Fact]
    public void Render_UsesLocalNameForNonEnglishCulture()
    {
        Assert.Equal("Local Name", NameRenderingHelper.Render("Local Name", "English Name", culture: new CultureInfo("bg-BG")));
    }

    [Fact]
    public void Render_UsesEnglishForFeiRulesetAndFallsBackToNameWhenBlank()
    {
        Assert.Equal(
            "English Name",
            NameRenderingHelper.Render("Local Name", "English Name", CompetitionRuleset.FEI, new CultureInfo("bg-BG"))
        );
        Assert.Equal(
            "Local Name",
            NameRenderingHelper.Render("Local Name", null, CompetitionRuleset.FEI, CultureInfo.InvariantCulture)
        );
    }

    [Fact]
    public void Render_FallsBackToEnglishWhenNameIsBlank()
    {
        Assert.Equal(
            "English Name",
            NameRenderingHelper.Render("", "English Name", culture: new CultureInfo("bg-BG"))
        );
    }
}
