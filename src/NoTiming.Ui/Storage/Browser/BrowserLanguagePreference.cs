using System.Globalization;
using Microsoft.JSInterop;
using Not.Injection;
using NoTiming.Ui.Features.Account;

namespace NoTiming.Ui.Storage.Browser;

/// <summary>
/// The language the person chose, in the local storage of the browser. A storage that cannot be read leaves the language of
/// the browser; one that cannot be written fails the choice, so that the person is told, as the app would otherwise load
/// again in the language they left.
/// </summary>
public sealed class BrowserLanguagePreference : ILanguagePreference, IScoped
{
    const string KEY = "nts.language";
    const string GET_ITEM = "localStorage.getItem";
    const string SET_ITEM = "localStorage.setItem";

    readonly IJSRuntime _browser;

    public BrowserLanguagePreference(IJSRuntime browser)
    {
        _browser = browser;
    }

    public string Current => Languages.CodeOf(CultureInfo.CurrentUICulture);

    public async Task Apply()
    {
        string? stored;
        try
        {
            stored = await _browser.InvokeAsync<string?>(GET_ITEM, KEY);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            return; // the storage is not there to be read: the language of the browser stays
        }

        if (Languages.Chosen(stored) is not { } code)
        {
            return;
        }

        var culture = CultureInfo.GetCultureInfo(code);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public async Task Choose(string code)
    {
        var chosen = Languages.Chosen(code) ?? throw new ArgumentException("The app is not written in that language.");
        await _browser.InvokeVoidAsync(SET_ITEM, KEY, chosen);
    }
}
