using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;
using Not.Injection;
using Not.Reflection;

namespace Not.Localization;

public static class LocalizationHelper
{
    public static string LocalizeString(string resource)
    {
        return Localize(resource);
    }

    public static string LocalizeEnum(Enum value)
    {
        try
        {
            var enumField = value.GetType().GetEnumField(value);
            var displayAttribute = enumField?.GetAttributes<DisplayAttribute>().FirstOrDefault();
            var name = displayAttribute?.Name;
            return name != null
                ? Localize(name)
                : value.ToString();
        }
# if DEBUG
        catch (InvalidOperationException ex) when (ex.Message.Contains("localization"))
        {
            Not.Notify.NotificationHelper.Current?.Error(ex);
            Not.Logging.LoggingHelper.Error(ex.ToString());
            return value.ToString();
        }
# else 
        catch (Exception)
        {
            return value.ToString();
        }
# endif
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

    static string Localize(string resource)
    {
        return _localizer != null
            ? _localizer[resource]
            : resource;
    }
}
