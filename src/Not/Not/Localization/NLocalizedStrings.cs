using Not.Reflection;
using System.ComponentModel;
using static Not.Localization.LocalizationHelper;

#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable NA0004 // Member spacing rule violation
namespace Not.Localization;

public static class NLocalizedStrings
{
    public static readonly string hash_string = LocalizeString("#");
    public static readonly string X_string = LocalizeString("X");
    public static readonly string Create_string = LocalizeString(nameof(Create_string));
    public static readonly string Update_string = LocalizeString(nameof(Update_string));
    public static readonly string Print_string = LocalizeString(nameof(Print_string));
    public static readonly string No__have_been_created_for_this_event_string = LocalizeString(nameof(No__have_been_created_for_this_event_string));
    public static readonly string Date_string = LocalizeString(nameof(Date_string));
    public static readonly string Time_string = LocalizeString(nameof(Time_string));
    public static readonly string Export_string = LocalizeString(nameof(Export_string));
    public static readonly string Print_Preview_string = LocalizeString(nameof(Print_Preview_string));
    public static readonly string Yes_string = LocalizeString(nameof(Yes_string));
    public static readonly string Cancel_string = LocalizeString(nameof(Cancel_string));

    public static string Localize(Enum value) // TODO: Use DisplayAttribute
    {
        var type = value.GetType();
        return
            ReflectionHelper
                .GetEnumField(type, value)
                ?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                .FirstOrDefault()
                is not DescriptionAttribute descriptionAttribute
            ? value.ToString()
            : descriptionAttribute.Description;
    }
}
#pragma warning restore NA0004
#pragma warning restore IDE1006
