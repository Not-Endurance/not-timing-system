namespace Not.Application.Authentication.Provider;

public static class NClientAuthenticationSessionPolicy
{
    static readonly TimeSpan DEFAULT_SESSION_LIFETIME = TimeSpan.FromHours(24);

    public static TimeSpan ResolveSessionLifetime(NClientAuthenticationSettings settings)
    {
        var sessionLifetime = settings.SessionLifetime;
        if (sessionLifetime.HasValue && sessionLifetime.Value > TimeSpan.Zero)
        {
            return sessionLifetime.Value;
        }

        return DEFAULT_SESSION_LIFETIME;
    }

    public static bool IsSessionActive(
        DateTimeOffset? startedAtUtc,
        NClientAuthenticationSettings settings,
        DateTimeOffset nowUtc
    )
    {
        if (!startedAtUtc.HasValue)
        {
            return false;
        }

        return startedAtUtc.Value.Add(ResolveSessionLifetime(settings)) > nowUtc;
    }
}
