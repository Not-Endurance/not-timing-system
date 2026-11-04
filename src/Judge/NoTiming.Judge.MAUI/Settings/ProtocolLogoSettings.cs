using NTS.Judge.Blazor.Features.Core.Rankings.Protocols;

namespace NTS.Judge.MAUI.Settings;

public sealed class ProtocolLogoSettings : IProtocolLogoState
{
    const string HEADER_LOGO_LEFT = "headerLogo.left";
    const string HEADER_LOGO_RIGHT = "headerLogo.right";
    const string LOGO_URL_DIRECTORY = "/images/logos";

    public string DirPath => Path.Combine(AppContext.BaseDirectory, "wwwroot", "images", "logos");

    public string Left
    {
        get => ResolveLogoUrl(Preferences.Default.Get(HEADER_LOGO_LEFT, "blank.png"));
        set => Preferences.Default.Set(HEADER_LOGO_LEFT, value);
    }

    public string Right
    {
        get => ResolveLogoUrl(Preferences.Default.Get(HEADER_LOGO_RIGHT, "blank.png"));
        set => Preferences.Default.Set(HEADER_LOGO_RIGHT, value);
    }

    public void SetLogo(string newLogo, string oldLogo)
    {
        if (IsSameLogo(oldLogo, Left))
        {
            Left = newLogo;
        }
        else if (IsSameLogo(oldLogo, Right))
        {
            Right = newLogo;
        }
    }

    static string ResolveLogoUrl(string logo)
    {
        return $"{LOGO_URL_DIRECTORY}/{Path.GetFileName(logo)}";
    }

    static bool IsSameLogo(string candidate, string selectedLogo)
    {
        return string.Equals(
            Path.GetFileName(candidate),
            Path.GetFileName(selectedLogo),
            StringComparison.OrdinalIgnoreCase
        );
    }
}
