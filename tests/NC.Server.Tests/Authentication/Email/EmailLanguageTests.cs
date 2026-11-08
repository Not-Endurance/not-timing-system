using NC.Server.Authentication.Email;
using Xunit;

namespace NC.Server.Tests.Authentication.Email;

public sealed class EmailLanguageTests
{
    [Fact]
    public void Choose_WhenBulgarian_ShouldReturnBulgarian()
    {
        Assert.Equal("bg", Packaged().Choose("bg-BG,bg;q=0.9,en;q=0.8"));
    }

    [Fact]
    public void Choose_WhenTurkishWithRegion_ShouldReturnTurkish()
    {
        Assert.Equal("tr", Packaged().Choose("tr-TR"));
    }

    [Fact]
    public void Choose_WhenHigherQualityIsTurkish_ShouldReturnTurkish()
    {
        Assert.Equal("tr", Packaged().Choose("bg;q=0.4,tr;q=0.9"));
    }

    [Fact]
    public void Choose_WhenOnlyUnsupportedLanguages_ShouldReturnEnglish()
    {
        Assert.Equal("en", Packaged().Choose("fr-FR,de;q=0.8"));
    }

    [Fact]
    public void Choose_WhenHeaderMissing_ShouldReturnEnglish()
    {
        Assert.Equal("en", Packaged().Choose(null));
    }

    [Fact]
    public void Choose_WhenLanguageIsNotPackaged_ShouldReturnEnglish()
    {
        var languages = new EmailLanguage(["en", "bg"]);

        Assert.Equal("en", languages.Choose("tr-TR"));
    }

    [Fact]
    public void Choose_WhenScriptTagIsPackaged_ShouldReturnThatTag()
    {
        Assert.Equal("zh-Hans", new EmailLanguage(["en", "zh-Hans"]).Choose("zh-Hans"));
    }

    [Fact]
    public void Choose_WhenRegionIsAddedToPackagedScript_ShouldReturnTheScriptTag()
    {
        Assert.Equal("zh-Hans", new EmailLanguage(["en", "zh-Hans"]).Choose("zh-Hans-CN"));
    }

    [Fact]
    public void Choose_WhenTagDiffersOnlyInCase_ShouldReturnThePackagedSpelling()
    {
        Assert.Equal("zh-Hans", new EmailLanguage(["en", "zh-Hans"]).Choose("ZH-hans"));
    }

    [Fact]
    public void Choose_WhenOnlyAnotherScriptIsPackaged_ShouldReturnEnglish()
    {
        Assert.Equal("en", new EmailLanguage(["en", "zh-Hant"]).Choose("zh-Hans"));
    }

    static EmailLanguage Packaged()
    {
        return new EmailLanguage(["en", "bg", "tr"]);
    }
}
