using System.Globalization;
using NoTiming.Ui.Features.Account;

namespace NTS.Tests.Unit.NoTiming.Ui.Features.Account;

/// <summary>
/// The languages the app is written in are English and Bulgarian (#645; the Turkish texts were taken away on 2026-10-23). A
/// person's choice of one is kept in the browser and is the culture of the app from the next time it starts; no choice leaves
/// the language of the browser, and a language the app is not written in is English, which every text falls back to.
/// </summary>
public sealed class LanguagesTests
{
    [Fact]
    public void The_app_is_written_in_English_and_Bulgarian_and_each_is_named_in_its_own_language()
    {
        Assert.Equal([("en", "English"), ("bg", "Български")], Languages.Supported.Select(x => (x.Code, x.Name)));
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("bg", "bg")]
    [InlineData("BG", "bg")]
    [InlineData(" bg ", "bg")]
    public void A_language_that_was_chosen_is_the_one_used(string stored, string expected)
    {
        Assert.Equal(expected, Languages.Chosen(stored));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("fr")]
    [InlineData("tr")] // the app is not written in Turkish any more
    [InlineData("bg-BG")]
    [InlineData("<script>")]
    public void What_was_not_chosen_from_the_languages_is_no_choice_and_the_language_of_the_browser_stays(
        string? stored
    )
    {
        Assert.Null(Languages.Chosen(stored));
    }

    [Theory]
    [InlineData("bg-BG", "bg")]
    [InlineData("bg", "bg")]
    [InlineData("tr-TR", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("en-US", "en")]
    [InlineData("fr-FR", "en")]
    [InlineData("", "en")]
    public void The_language_in_use_is_the_one_the_culture_is_of_or_English_when_the_app_is_not_written_in_it(
        string culture,
        string expected
    )
    {
        Assert.Equal(expected, Languages.CodeOf(CultureInfo.GetCultureInfo(culture)));
    }
}
