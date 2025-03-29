using Microsoft.Extensions.Localization;
using Not.Injection;

namespace Not.Localization;

public static class LocalizationHelper
{
    static readonly IStringLocalizer LOCALIZER = ServiceLocator.Get<IStringLocalizer>();

    //public static string Localize(string text)
    //{
    //    return LOCALIZER[text];
    //}

    public static string LocalizeString(string text, params object[] args)
    {
        var localized = args.Select(x => LOCALIZER[x.ToString() ?? ""]);
        return string.Format(LOCALIZER[text], localized);
    }
}
