using System.Globalization;

namespace NC.Localization;

/// <summary>
/// Sets the UI culture for the scope, so localized strings read in that language. The previous culture returns on dispose.
/// </summary>
public sealed class CultureScope : IDisposable
{
    readonly CultureInfo _previous;

    public CultureScope(string culture)
    {
        _previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
    }

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _previous;
    }
}
