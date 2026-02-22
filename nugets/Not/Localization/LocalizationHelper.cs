using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;
using Not.Injection;
using Not.Notify;
using Not.Reflection;

namespace Not.Localization;

public static class LocalizationHelper
{
    static readonly IStringLocalizer LOCALIZER = ServiceLocator.Get<IStringLocalizer>();
    static readonly INotifier NOTIFIER = ServiceLocator.Get<INotifier>();

    public static string LocalizeString(string resource)
    {
        return LOCALIZER[resource];
    }

    public static string LocalizeEnum(Enum value)
    {
        try
        {
            return value.GetType().GetEnumField(value)?.GetAttributes<DisplayAttribute>().FirstOrDefault()?.GetName()
                ?? value.ToString();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("localization"))
        {
            var message =
                Text_formatting_failed_This_is_usually_not_critical_failure_string
                + Environment.NewLine
                + $"Localization resource is missing key for '{value}'";
            NOTIFIER.Error(message);
            return value.ToString();
        }
    }
}
