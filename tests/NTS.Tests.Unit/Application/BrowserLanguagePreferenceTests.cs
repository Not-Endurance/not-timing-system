using Microsoft.JSInterop;
using NoTiming.Ui.Storage.Browser;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// The language a person chooses is kept in the local storage of the browser (#645). A choice that the browser cannot keep
/// is not taken for made, so that the person is told and the app is not loaded again in the language they left; a language
/// the app is not written in is refused before anything is written.
/// </summary>
public sealed class BrowserLanguagePreferenceTests
{
    [Theory]
    [InlineData("bg", "bg")]
    [InlineData("TR", "tr")]
    [InlineData(" en ", "en")]
    public async Task A_language_that_is_chosen_is_written_to_the_storage_of_the_browser(string chosen, string written)
    {
        var browser = new FakeLocalStorage();

        await new BrowserLanguagePreference(browser).Choose(chosen);

        var call = Assert.Single(browser.Calls);
        Assert.Equal("localStorage.setItem", call.Identifier);
        Assert.Equal(["nts.language", written], call.Arguments);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData("bg-BG")]
    public async Task A_language_the_app_is_not_written_in_is_refused_and_nothing_is_written(string chosen)
    {
        var browser = new FakeLocalStorage();

        await Assert.ThrowsAsync<ArgumentException>(() => new BrowserLanguagePreference(browser).Choose(chosen));

        Assert.Empty(browser.Calls);
    }

    [Fact]
    public async Task A_choice_the_browser_cannot_keep_fails_and_is_not_taken_for_made()
    {
        var browser = new FakeLocalStorage { Fails = new JSException("The quota has been exceeded.") };

        await Assert.ThrowsAsync<JSException>(() => new BrowserLanguagePreference(browser).Choose("bg"));
    }
}
