using NC.Server.Authentication.Email;
using Xunit;

namespace NC.Tests.Authentication.Email;

public sealed class EmailLanguageTests
{
    [Fact]
    public void Choose_WhenBulgarian_ShouldReturnBulgarian()
    {
        Assert.Equal("bg", EmailLanguage.Choose("bg-BG,bg;q=0.9,en;q=0.8"));
    }

    [Fact]
    public void Choose_WhenTurkishWithRegion_ShouldReturnTurkish()
    {
        Assert.Equal("tr", EmailLanguage.Choose("tr-TR"));
    }

    [Fact]
    public void Choose_WhenHigherQualityIsTurkish_ShouldReturnTurkish()
    {
        Assert.Equal("tr", EmailLanguage.Choose("bg;q=0.4,tr;q=0.9"));
    }

    [Fact]
    public void Choose_WhenOnlyUnsupportedLanguages_ShouldReturnEnglish()
    {
        Assert.Equal("en", EmailLanguage.Choose("fr-FR,de;q=0.8"));
    }

    [Fact]
    public void Choose_WhenHeaderMissing_ShouldReturnEnglish()
    {
        Assert.Equal("en", EmailLanguage.Choose(null));
    }
}
