using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;
using Not.Injection;
using Not.Notify;
using Not.Reflection;

namespace Not.Localization;

public static class LocalizationHelper
{
    public static string LocalizeString(string resource)
    {
        TryInitialize();
        return _localizer != null ? _localizer[resource] : resource;
    }

    public static string LocalizeEnum(Enum value)
    {
        TryInitialize();
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
            _notifier?.Error(message);
            return value.ToString();
        }
    }

    static IStringLocalizer? _localizer;
    static INotifier? _notifier;

    static void TryInitialize()
    {
        _localizer ??= ServiceLocator.Get<IStringLocalizer>();
        _notifier ??= ServiceLocator.Get<INotifier>();
    }
}
