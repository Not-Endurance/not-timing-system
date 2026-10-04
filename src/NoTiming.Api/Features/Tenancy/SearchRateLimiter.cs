using Microsoft.Extensions.Options;
using NoTiming.Api.Features.Account;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>
/// How often the accounts may be searched (#643, ADR-0012), in a window of ten minutes by default: so many searches for
/// one account, and so many for the whole platform, which is what stops one person, or a few, from reading the
/// accounts out one name at a time. Both are settings of the host, per host instance like the limits of signing in.
/// </summary>
internal sealed class SearchRateLimitOptions
{
    public const string SECTION = "Search:RateLimits";

    public static bool IsValid(SearchRateLimitOptions options)
    {
        return options.Window > TimeSpan.Zero && options.PerAccount >= 1 && options.Overall >= 1;
    }

    /// <summary>Searches made by one account in a window.</summary>
    public int PerAccount { get; set; } = 30;

    /// <summary>Searches made by everybody in a window.</summary>
    public int Overall { get; set; } = 600;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(10);
}

/// <summary>
/// Counts the searches of accounts against the budgets of the account that asks and of the platform. A search that is
/// refused costs nothing, and a request that never reaches the search, because it asked wrongly or was not allowed to,
/// costs nothing either: it is the ones that read the accounts that are counted.
/// </summary>
internal sealed class SearchRateLimiter
{
    readonly RateLedger _ledger;
    readonly SearchRateLimitOptions _options;
    readonly EventThrottle _throttle;
    readonly ILogger<SearchRateLimiter> _logger;

    public SearchRateLimiter(
        IOptions<SearchRateLimitOptions> options,
        TimeProvider time,
        EventThrottle throttle,
        ILogger<SearchRateLimiter> logger
    )
    {
        _options = options.Value;
        _ledger = new RateLedger(time);
        _throttle = throttle;
        _logger = logger;
    }

    /// <summary>Takes the cost of a search by the account; null when there was room for it.</summary>
    public RateLimitRefusal? TrySearch(Guid account)
    {
        var refusal = _ledger.TryTake(
            [
                new RateBudget("search/account", $"search|account|{account}", _options.PerAccount),
                new RateBudget("search/overall", "search|overall", _options.Overall),
            ],
            _options.Window
        );
        if (
            refusal != null
            && _throttle.ShouldLog($"{AuthEvents.SEARCH_RATE_LIMITED.Name}/{refusal.Scope}", out var leftOut)
        )
        {
            _logger.LogWarning(
                AuthEvents.SEARCH_RATE_LIMITED,
                "A search of accounts was refused: the {Scope} limit is spent, for another {RetryAfter} s. {LeftOut} refused since the last report.",
                refusal.Scope,
                (int)refusal.RetryAfter.TotalSeconds,
                leftOut
            );
        }

        return refusal;
    }
}
