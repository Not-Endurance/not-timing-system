namespace Not.Server.UiClient;

// The path a client is served under, as a leading-slash path without a trailing slash, for example "/ui".
internal static class UiPath
{
    public static string Normalize(string prefix)
    {
        var trimmed = prefix.Trim().Trim('/');
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("A UI path prefix is required, for example \"ui\".", nameof(prefix));
        }

        return "/" + trimmed;
    }
}
