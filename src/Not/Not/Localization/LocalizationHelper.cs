using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;
using Not.Injection;
using Not.Notify;
using Not.Reflection;

namespace Not.Localization;

public static class LocalizationHelper
{
    static readonly IStringLocalizer LOCALIZER = ServiceLocator.Get<IStringLocalizer>();

    public static string LocalizeString(string resource, params object[] args)
    {
        try
        {
            var localized = args.Select(x => LOCALIZER[x.ToString() ?? ""]);
            return string.Format(LOCALIZER[resource], localized);
        }
        catch (FormatException)
        {
            var message = Text_formatting_failed_This_is_usually_not_critical_failure_string
                + Environment.NewLine
                + $"Format: {resource}"
                +Environment.NewLine
                + $"args: {string.Join(", ", args)}";
            NotifyHelper.Error(message);
            return resource;
        }
    }

    public static string LocalizeEnum(Enum value)
    {
        try
        {
            return value.GetType()
                       .GetEnumField(value)
                       ?.GetAttributes<DisplayAttribute>()
                       .FirstOrDefault()
                       ?.GetName()
                   ?? value.ToString();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("localization"))
        {
            var message = Text_formatting_failed_This_is_usually_not_critical_failure_string
                          + Environment.NewLine
                          + $"Localization resource is missing key for '{value}'";
            NotifyHelper.Error(message);
            return value.ToString();
        }
    }
}
