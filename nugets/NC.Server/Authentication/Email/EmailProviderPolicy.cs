namespace NC.Server.Authentication.Email;

public static class EmailProviderPolicy
{
    /// <summary>Production never prints or stores codes, so the console and outbox senders are refused there.</summary>
    public static string? Problem(EmailProviderKind provider, bool production)
    {
        if (production && (provider == EmailProviderKind.Console || provider == EmailProviderKind.Outbox))
        {
            return $"Production must not send email through the {provider} provider.";
        }

        return null;
    }
}
