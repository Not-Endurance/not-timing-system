namespace NC.Server.Authentication.Codes;

/// <summary>
/// The limits on sending codes: sixty seconds between codes to one address, and five per address, ten per IP address
/// and one hundred overall in each hour. Limits count the hour bucket, so a refusal lasts until the next hour begins.
/// </summary>
internal static class CodeSendLimits
{
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);
    public static readonly int PerAddressPerHour = 5;
    public static readonly int PerIpAddressPerHour = 10;
    public static readonly int OverallPerHour = 100;

    public static SendDecision Decide(SendHistory history, DateTimeOffset now)
    {
        if (history.LastSentForAddressUtc is { } lastSent && now - lastSent < Cooldown)
        {
            return Refuse(SendLimitScope.Cooldown, Cooldown - (now - lastSent));
        }

        var untilNextHour = NextHourStart(now) - now;
        if (history.ForAddressThisHour >= PerAddressPerHour)
        {
            return Refuse(SendLimitScope.Address, untilNextHour);
        }

        if (history.ForIpThisHour >= PerIpAddressPerHour)
        {
            return Refuse(SendLimitScope.IpAddress, untilNextHour);
        }

        if (history.OverallThisHour >= OverallPerHour)
        {
            return Refuse(SendLimitScope.Overall, untilNextHour);
        }

        return new SendDecision
        {
            Allowed = true,
            Scope = null,
            RetryAfter = TimeSpan.Zero,
        };
    }

    static SendDecision Refuse(SendLimitScope scope, TimeSpan retryAfter)
    {
        return new SendDecision
        {
            Allowed = false,
            Scope = scope,
            RetryAfter = retryAfter,
        };
    }

    static DateTimeOffset NextHourStart(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero).AddHours(1);
    }
}
