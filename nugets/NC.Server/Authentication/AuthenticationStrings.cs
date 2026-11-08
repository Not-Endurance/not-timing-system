using static NC.Localization.LocalizationHelper;

// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006 // Naming Styles

namespace NC.Server.Authentication;

/// <summary>
/// The texts of the emails sign-in sends. The values live in the host's resource files, so each language is read from
/// the resource file of that language. A language is read under a <see cref="NC.Localization.CultureScope"/>.
/// </summary>
internal static class AuthenticationStrings
{
    public static string Your_sign_in_code_string => LocalizeString(nameof(Your_sign_in_code_string));
    public static string Your_sign_in_code_is_0_string => LocalizeString(nameof(Your_sign_in_code_is_0_string));
    public static string Your_registration_code_string => LocalizeString(nameof(Your_registration_code_string));
    public static string Your_registration_code_is_0_string =>
        LocalizeString(nameof(Your_registration_code_is_0_string));
    public static string A_passkey_was_added_to_your_account_string =>
        LocalizeString(nameof(A_passkey_was_added_to_your_account_string));
    public static string A_passkey_named_1_was_added_to_your_account_on_0_string =>
        LocalizeString(nameof(A_passkey_named_1_was_added_to_your_account_on_0_string));
}

#pragma warning restore IDE1006
