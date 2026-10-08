namespace NoTiming.Ui.Features.Account;

/// <summary>
/// The language of the app as the person chose it (#645). It is a preference of the browser and not of the account: it is
/// kept there, read when the app starts and before it renders, and nothing of it reaches the host. A person who has not
/// chosen has the language of the browser.
/// </summary>
public interface ILanguagePreference
{
    /// <summary>The language the app is in now, one of <see cref="Languages.Supported"/>.</summary>
    string Current { get; }

    /// <summary>Makes the language the person chose the culture of the app. Called before the app renders.</summary>
    Task Apply();

    /// <summary>
    /// Keeps the language the person chose. The texts are written when a page is rendered, so the app is loaded again to
    /// have all of them in it.
    /// </summary>
    Task Choose(string code);
}
