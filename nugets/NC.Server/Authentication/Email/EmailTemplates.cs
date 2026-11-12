using System.Globalization;
using NC.Localization;

namespace NC.Server.Authentication.Email;

/// <summary>The emails that sign-in sends. Each is written in the language it is asked for, in the host's resources.</summary>
internal static class EmailTemplates
{
    public static EmailMessage SignInCode(string to, string language, string code)
    {
        using var _ = new CultureScope(language);
        return new EmailMessage
        {
            To = to,
            Subject = AuthenticationStrings.Your_sign_in_code_string,
            TextBody = string.Format(
                CultureInfo.InvariantCulture,
                AuthenticationStrings.Your_sign_in_code_is_0_string,
                code
            ),
        };
    }

    public static EmailMessage RegistrationCode(string to, string language, string code)
    {
        using var _ = new CultureScope(language);
        return new EmailMessage
        {
            To = to,
            Subject = AuthenticationStrings.Your_registration_code_string,
            TextBody = string.Format(
                CultureInfo.InvariantCulture,
                AuthenticationStrings.Your_registration_code_is_0_string,
                code
            ),
        };
    }

    public static EmailMessage PasskeyAdded(string to, string language, string passkeyName, DateTimeOffset addedUtc)
    {
        using var _ = new CultureScope(language);
        return new EmailMessage
        {
            To = to,
            Subject = AuthenticationStrings.A_passkey_was_added_to_your_account_string,
            TextBody = string.Format(
                CultureInfo.InvariantCulture,
                AuthenticationStrings.A_passkey_named_1_was_added_to_your_account_on_0_string,
                addedUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC",
                passkeyName
            ),
        };
    }
}
