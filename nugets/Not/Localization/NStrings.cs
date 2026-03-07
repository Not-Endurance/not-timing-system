using static Not.Localization.LocalizationHelper;
// ReSharper disable InconsistentNaming

#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable NA0004 // Member spacing rule violation
namespace Not.Localization;

public static class NStrings
{
    public static string Localize(Enum value)
    {
        return LocalizeEnum(value);
    }

    public static string hash_string => LocalizeString("#");
    public static string X_string => LocalizeString("X");
    public static string Create_string => LocalizeString(nameof(Create_string));
    public static string Back_string => LocalizeString(nameof(Back_string));
    public static string Update_string => LocalizeString(nameof(Update_string));
    public static string Updated_string => LocalizeString(nameof(Updated_string));
    public static string Delete_string => LocalizeString(nameof(Delete_string));
    public static string Print_string => LocalizeString(nameof(Print_string));
    public static string Date_string => LocalizeString(nameof(Date_string));
    public static string Time_string => LocalizeString(nameof(Time_string));
    public static string Export_string => LocalizeString(nameof(Export_string));
    public static string Print_Preview_string => LocalizeString(nameof(Print_Preview_string));
    public static string Yes_string => LocalizeString(nameof(Yes_string));
    public static string Cancel_string => LocalizeString(nameof(Cancel_string));
    public static string Field_is_required_string => LocalizeString(nameof(Field_is_required_string));
    public static string List_is_empty_string => LocalizeString(nameof(List_is_empty_string));
    public static string Add_string => LocalizeString(nameof(Add_string));
    public static string Are_you_sure_you_want_to_delete__string =>
        LocalizeString(nameof(Are_you_sure_you_want_to_delete__string));
    public static string Could_not_connect_to_Nexus_Some_operations_will_not_be_available_Please_check_your_internet_connection =>
        LocalizeString(
            nameof(
                Could_not_connect_to_Nexus_Some_operations_will_not_be_available_Please_check_your_internet_connection
            )
        );
    public static string Text_formatting_failed_This_is_usually_not_critical_failure_string =>
        LocalizeString(nameof(Text_formatting_failed_This_is_usually_not_critical_failure_string));
    public static string Language_string => LocalizeString(nameof(Language_string));
    public static string Upload_string => LocalizeString(nameof(Upload_string));
    public static string Delete_selection_string => LocalizeString(nameof(Delete_selection_string));
    public static string Image_browser_string => LocalizeString(nameof(Image_browser_string));
    public static string Image_browser_empty_string => LocalizeString(nameof(Image_browser_empty_string));
    public static string Use_image_string => LocalizeString(nameof(Use_image_string));
    public static string Empty_string => LocalizeString(nameof(Empty_string));
    public static string Sign_in_to_continue_string => LocalizeString(nameof(Sign_in_to_continue_string));
    public static string Sign_in_with_Microsoft_string => LocalizeString(nameof(Sign_in_with_Microsoft_string));
}
#pragma warning restore NA0004
#pragma warning restore IDE1006
