namespace Not.Blazor.Client.Authentication;

public static class AuthenticationContents
{
    public const string AUTHENTICATION = "authentication";
    public const string AUTHENTICATION_REDIRECT = "authentication/{action}";

    /// <summary>
    /// The authentication routes are transient: they only exist to drive a sign-in or sign-out
    /// round trip. Returning to one of them once the round trip is over just re-runs it, so callers
    /// use this to keep an authentication route out of a return URL.
    /// </summary>
    public static bool IsAuthenticationRoute(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var absoluteUri))
        {
            return false;
        }

        // Match the route segment rather than a prefix, so only "authentication" and its actions
        // count and a future route such as "authentication-help" does not.
        var firstSegment = absoluteUri.AbsolutePath.Trim('/').Split('/')[0];
        return string.Equals(firstSegment, AUTHENTICATION, StringComparison.OrdinalIgnoreCase);
    }
}
