namespace NC.Server.Authentication.Identity;

internal static class EmailAddress
{
    /// <summary>The form every address is stored and matched in: trimmed and lower case.</summary>
    public static string Normalize(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
