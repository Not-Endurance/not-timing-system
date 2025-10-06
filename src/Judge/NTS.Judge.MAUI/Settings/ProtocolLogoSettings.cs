using NTS.Judge.Blazor.Core.Rankings.Protocols;

namespace NTS.Judge.MAUI.Settings;

public sealed class ProtocolLogoSettings : IProtocolLogoState
{
    const string HEADER_LOGO_LEFT = "headerLogo.left";
    const string HEADER_LOGO_RIGHT = "headerLogo.right";
    const string SHORT_PATH = "images\\logos";

    public string DirPath => "wwwroot\\"+SHORT_PATH;

    public string Left
    {
        get => Path.Combine(SHORT_PATH, Path.GetFileName(Preferences.Default.Get(HEADER_LOGO_LEFT, "blank.png")));
        set => Preferences.Default.Set(HEADER_LOGO_LEFT, value);
    }

    public string Right
    {
        get => Path.Combine(SHORT_PATH, Path.GetFileName(Preferences.Default.Get(HEADER_LOGO_RIGHT, "blank.png")));
        set => Preferences.Default.Set(HEADER_LOGO_RIGHT, value);
    }

    public void SetLogo(string newLogo, string oldLogo)
    {
        if(oldLogo == Left)
        {
            Left = newLogo;
        }
        else
        {
            Right = newLogo;
        }
    }
}
