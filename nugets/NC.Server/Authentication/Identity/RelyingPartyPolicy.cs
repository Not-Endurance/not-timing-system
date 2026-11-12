namespace NC.Server.Authentication.Identity;

/// <summary>
/// Checks the passkey relying-party ID against the app origin at startup. The app origin must be a subdomain of the ID,
/// and production must not use a non-production ID.
/// </summary>
internal static class RelyingPartyPolicy
{
    public static string? Problem(string relyingPartyId, string appOrigin, bool production)
    {
        if (!Uri.TryCreate(appOrigin, UriKind.Absolute, out var origin))
        {
            return "The app origin is not an absolute URL.";
        }

        var rpId = relyingPartyId.Trim().ToLowerInvariant();
        var host = origin.Host.ToLowerInvariant();
        if (!host.EndsWith("." + rpId, StringComparison.Ordinal))
        {
            return "The app origin must be a subdomain of the relying-party ID.";
        }

        if (production && (rpId == "localhost" || rpId.StartsWith("staging.", StringComparison.Ordinal)))
        {
            return "Production must not use a non-production relying-party ID.";
        }

        return null;
    }
}
