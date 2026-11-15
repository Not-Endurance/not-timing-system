using NC.Server.Authentication.Codes;

namespace NC.Server.Authentication.RateLimits;

/// <summary>Reads the counters and the last send for an address, and records a send once it is allowed.</summary>
internal sealed class CodeSendGate
{
    const string ADDRESS_SCOPE = "address";
    const string IP_SCOPE = "ip";
    const string OVERALL_SCOPE = "overall";

    readonly IRateCounterRepository _counters;
    readonly ICodeChallengeRepository _challenges;

    public CodeSendGate(IRateCounterRepository counters, ICodeChallengeRepository challenges)
    {
        _counters = counters;
        _challenges = challenges;
    }

    public async Task<SendDecision> Review(
        string email,
        string clientAddress,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var hour = HourStart(now);
        var challenge = await _challenges.Find(email, cancellationToken);
        var history = new SendHistory
        {
            ForAddressThisHour = await _counters.Count(ADDRESS_SCOPE, email, hour, cancellationToken),
            ForIpThisHour = await _counters.Count(IP_SCOPE, clientAddress, hour, cancellationToken),
            OverallThisHour = await _counters.Count(OVERALL_SCOPE, "all", hour, cancellationToken),
            LastSentForAddressUtc = challenge is null
                ? null
                : new DateTimeOffset(challenge.LastSentAtUtc, TimeSpan.Zero),
        };

        return CodeSendLimits.Decide(history, now);
    }

    public async Task Record(
        string email,
        string clientAddress,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var hour = HourStart(now);
        await _counters.Increment(ADDRESS_SCOPE, email, hour, cancellationToken);
        await _counters.Increment(IP_SCOPE, clientAddress, hour, cancellationToken);
        await _counters.Increment(OVERALL_SCOPE, "all", hour, cancellationToken);
    }

    static DateTimeOffset HourStart(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }
}
