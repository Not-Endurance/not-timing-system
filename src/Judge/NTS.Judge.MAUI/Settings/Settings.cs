using NTS.Judge.Blazor.Core.Rankings.Protocols;

namespace NTS.Judge.MAUI.Settings;

public sealed class Settings : ILogoPersistence
{
    const string HEADER_LOGO_LEFT = "headerLogo.left";
    const string HEADER_LOGO_RIGHT = "headerLogo.right";

    public string Left
    {
        get => Preferences.Default.Get(HEADER_LOGO_LEFT, "blank.png");
        set => Preferences.Default.Set(HEADER_LOGO_LEFT, value);
    }
    public string Right
    {
        get => Preferences.Default.Get(HEADER_LOGO_RIGHT, "blank.png");
        set => Preferences.Default.Set(HEADER_LOGO_RIGHT, value);
    }
}
