namespace NC.Server.Authentication.Sessions;

/// <summary>A session lasts thirty days from its last extension and is extended at most once an hour.</summary>
public static class SessionRules
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);
    public static readonly TimeSpan ExtensionInterval = TimeSpan.FromHours(1);

    public static bool ShouldExtend(DateTimeOffset lastExtendedUtc, DateTimeOffset now)
    {
        return now - lastExtendedUtc >= ExtensionInterval;
    }

    public static DateTimeOffset ExpiresAt(DateTimeOffset now)
    {
        return now.Add(Lifetime);
    }
}
