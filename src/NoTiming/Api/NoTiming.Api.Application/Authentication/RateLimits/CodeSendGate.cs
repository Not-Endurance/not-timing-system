using NoTiming.Api.Application.Authentication.Codes;

namespace NoTiming.Api.Application.Authentication.RateLimits;

/// <summary>Reads the counters and the last send for an address, and records a send once it is allowed.</summary>
public sealed class CodeSendGate
{
    const string ADDRESS_SCOPE = "address";
    const string IP_SCOPE = "ip";
    const string OVERALL_SCOPE = "overall";

    readonly RateCounterStore _counters;
    readonly CodeChallengeStore _challenges;

    public CodeSendGate(RateCounterStore counters, CodeChallengeStore challenges)
    {
        _counters = counters;
        _challenges = challenges;
    }

    public async Task<SendDecision> ReviewAsync(
        string email,
        string clientAddress,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var hour = HourStart(now);
        var challenge = await _challenges.FindAsync(email, cancellationToken);
        var history = new SendHistory
        {
            ForAddressThisHour = await _counters.CountAsync(ADDRESS_SCOPE, email, hour, cancellationToken),
            ForIpThisHour = await _counters.CountAsync(IP_SCOPE, clientAddress, hour, cancellationToken),
            OverallThisHour = await _counters.CountAsync(OVERALL_SCOPE, "all", hour, cancellationToken),
            LastSentForAddressUtc = challenge is null ? null : new DateTimeOffset(challenge.LastSentAt, TimeSpan.Zero),
        };

        return CodeSendLimits.Decide(history, now);
    }

    public async Task RecordAsync(
        string email,
        string clientAddress,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var hour = HourStart(now);
        await _counters.IncrementAsync(ADDRESS_SCOPE, email, hour, cancellationToken);
        await _counters.IncrementAsync(IP_SCOPE, clientAddress, hour, cancellationToken);
        await _counters.IncrementAsync(OVERALL_SCOPE, "all", hour, cancellationToken);
    }

    static DateTimeOffset HourStart(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }
}
