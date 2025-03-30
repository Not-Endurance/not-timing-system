using Microsoft.Extensions.Localization;
using Not.Injection;
using Not.Notify;

namespace Not.Localization;

public static class LocalizationHelper
{
    static readonly IStringLocalizer LOCALIZER = ServiceLocator.Get<IStringLocalizer>();

    public static string LocalizeString(string format, params object[] args)
    {
        try
        {
            var localized = args.Select(x => LOCALIZER[x.ToString() ?? ""]);
            return string.Format(LOCALIZER[format], localized);
        }
        catch (FormatException)
        {
            var message = Text_formatting_failed_This_is_usually_not_critical_failure_string
                + Environment.NewLine
                + $"Format: {format}"
                +Environment.NewLine
                + $"args: {string.Join(", ", args)}";
            NotifyHelper.Error(message);
            return format;
        }
    }
}
