using static Not.Localization.LocalizationHelper;
// ReSharper disable InconsistentNaming

#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable NA0004 // Member spacing rule violation
namespace Not.Localization;

public static class NStrings
{
    public static string Localize(Enum value) // TODO: Use DisplayAttribute
    {
        return LocalizeEnum(value);
    }

    public static string hash_string => LocalizeString("#");
    public static string X_string => LocalizeString("X");
    public static string Create_string => LocalizeString(nameof(Create_string));
    public static string Update_string => LocalizeString(nameof(Update_string));
    public static string Print_string => LocalizeString(nameof(Print_string));
    public static string No__have_been_created_for_this_event_string =>
        LocalizeString(nameof(No__have_been_created_for_this_event_string));
    public static string Date_string => LocalizeString(nameof(Date_string));
    public static string Time_string => LocalizeString(nameof(Time_string));
    public static string Export_string => LocalizeString(nameof(Export_string));
    public static string Print_Preview_string => LocalizeString(nameof(Print_Preview_string));
    public static string Yes_string => LocalizeString(nameof(Yes_string));
    public static string Cancel_string => LocalizeString(nameof(Cancel_string));
    public static string Field_is_required_string => LocalizeString(nameof(Field_is_required_string));
    public static string Could_not_connect_to_Nexus_Some_operations_will_not_be_available_Please_check_your_internet_connection =>
        LocalizeString(
            nameof(
                Could_not_connect_to_Nexus_Some_operations_will_not_be_available_Please_check_your_internet_connection
            )
        );
    public static string Text_formatting_failed_This_is_usually_not_critical_failure_string =>
        LocalizeString(nameof(Text_formatting_failed_This_is_usually_not_critical_failure_string));
    public static string Language_string => LocalizeString(nameof(Language_string));
}
#pragma warning restore NA0004
#pragma warning restore IDE1006
