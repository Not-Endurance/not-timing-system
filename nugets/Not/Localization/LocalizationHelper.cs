using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;
using Not.Logging;
using Not.Injection;
using Not.Notify;
using Not.Reflection;

namespace Not.Localization;

public static class LocalizationHelper
{
    public static string LocalizeString(string resource)
    {
        return _localizer != null ? _localizer[resource] : resource;
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
            NotificationHelper.Current?.Error(message);
            LoggingHelper.Error(message);
            return value.ToString();
        }
    }

    public static void Configure(IStringLocalizer? localizer)
    {
        _localizer = localizer;
    }

    public static void Clear(IStringLocalizer? localizer = null)
    {
        if (localizer == null || ReferenceEquals(_localizer, localizer))
        {
            _localizer = null;
        }
    }

    static IStringLocalizer? _localizer;
}
