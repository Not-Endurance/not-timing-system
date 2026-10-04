using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// The limits of asking for a code and of trying one (#601, ADR-0002), each over a window of an hour, in memory and
/// per host: the budgets of two hosts are not shared, so a deployment of several multiplies them. Every limit is a
/// setting; the starting values are 5 an hour for an address, 10 for a client and 100 for the platform.
/// </summary>
internal sealed class AuthRateLimitOptions
{
    public const string SECTION = "Auth:RateLimits";

    public static bool IsValid(AuthRateLimitOptions options)
    {
        return options.Window > TimeSpan.Zero
            && new[]
            {
                options.SendsPerAddress,
                options.SendsPerClient,
                options.SendsOverall,
                options.FailedVerificationsPerAddress,
                options.FailedVerificationsPerClient,
                options.FailedVerificationsOverall,
            }.All(x => x >= 1);
    }

    /// <summary>Requests for a code, sign-in or registration alike, that name one address.</summary>
    public int SendsPerAddress { get; set; } = 5;

    /// <summary>Requests for a code from one client, whatever the addresses.</summary>
    public int SendsPerClient { get; set; } = 10;

    public int SendsOverall { get; set; } = 100;

    /// <summary>
    /// Wrong codes for one address from one client. They are counted against the address as that client tries it,
    /// never against the address alone: nobody can lock a person out of their own address by guessing at it.
    /// </summary>
    public int FailedVerificationsPerAddress { get; set; } = 5;

    public int FailedVerificationsPerClient { get; set; } = 10;
    public int FailedVerificationsOverall { get; set; } = 100;

    public TimeSpan Window { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>What stood in the way of a request, and how long until it would not.</summary>
internal sealed class RateLimitRefusal
{
    public RateLimitRefusal(string scope, TimeSpan retryAfter)
    {
        Scope = scope;
        RetryAfter = retryAfter;
    }

    /// <summary>The budget that was spent, such as <c>send/address</c>. It names no address and no client.</summary>
    public string Scope { get; }

    public TimeSpan RetryAfter { get; }
}

/// <summary>
/// Counts what each request costs against the budgets it draws on and refuses it when one is spent. A refused request
/// costs nothing, so a window ends when it was due to. What a request draws on does not depend on whether its address
/// has an account, so a refusal tells nothing about that. Sending a code costs one of each of its three budgets. A
/// wrong code costs one of each of its three, and a right one costs nothing: the cost is taken before the code is
/// looked at, and handed back by <see cref="Succeeded"/>.
/// </summary>
internal sealed class AuthRateLimiter
{
    static readonly TimeSpan SWEEP_EVERY = TimeSpan.FromMinutes(1);

    readonly object _lock = new();
    readonly Dictionary<string, Window> _windows = new();
    readonly AuthRateLimitOptions _options;
    readonly ILookupNormalizer _normalizer;
    readonly TimeProvider _time;
    readonly EventThrottle _throttle;
    readonly ILogger<AuthRateLimiter> _logger;
    DateTimeOffset _nextSweep;

    public AuthRateLimiter(
        IOptions<AuthRateLimitOptions> options,
        ILookupNormalizer normalizer,
        TimeProvider time,
        EventThrottle throttle,
        ILogger<AuthRateLimiter> logger
    )
    {
        _options = options.Value;
        _normalizer = normalizer;
        _time = time;
        _throttle = throttle;
        _logger = logger;
    }

    /// <summary>Takes the cost of sending a code to the address; null when there was room for it.</summary>
    public RateLimitRefusal? TrySend(string email, string client)
    {
        var address = AddressOf(email);
        return Take(
            [
                new Budget("send/address", $"send|address|{address}", _options.SendsPerAddress),
                new Budget("send/client", $"send|client|{client}", _options.SendsPerClient),
                new Budget("send/overall", "send|overall", _options.SendsOverall),
            ]
        );
    }

    /// <summary>Takes the cost of an attempt at a code; null when there was room for it.</summary>
    public RateLimitRefusal? TryVerify(string email, string client)
    {
        return Take(VerificationBudgets(email, client));
    }

    /// <summary>Gives back what <see cref="TryVerify"/> took, for a code that was right.</summary>
    public void Succeeded(string email, string client)
    {
        lock (_lock)
        {
            var now = _time.GetUtcNow();
            foreach (var budget in VerificationBudgets(email, client))
            {
                if (_windows.TryGetValue(budget.Key, out var window) && window.ResetsAt > now && window.Count > 0)
                {
                    window.Count--;
                }
            }
        }
    }

    Budget[] VerificationBudgets(string email, string client)
    {
        var address = AddressOf(email);
        return
        [
            new Budget("verify/address", $"verify|address|{address}|{client}", _options.FailedVerificationsPerAddress),
            new Budget("verify/client", $"verify|client|{client}", _options.FailedVerificationsPerClient),
            new Budget("verify/overall", "verify|overall", _options.FailedVerificationsOverall),
        ];
    }

    RateLimitRefusal? Take(Budget[] budgets)
    {
        lock (_lock)
        {
            var now = _time.GetUtcNow();
            Sweep(now);

            RateLimitRefusal? refusal = null;
            foreach (var budget in budgets)
            {
                if (!_windows.TryGetValue(budget.Key, out var window) || window.ResetsAt <= now)
                {
                    continue;
                }

                if (window.Count >= budget.Limit && (refusal is null || window.ResetsAt - now > refusal.RetryAfter))
                {
                    refusal = new RateLimitRefusal(budget.Scope, window.ResetsAt - now);
                }
            }

            if (refusal != null)
            {
                if (_throttle.ShouldLog($"{AuthEvents.RATE_LIMITED.Name}/{refusal.Scope}", out var leftOut))
                {
                    _logger.LogWarning(
                        AuthEvents.RATE_LIMITED,
                        "A request was refused: the {Scope} limit is spent, for another {RetryAfter} s. {LeftOut} refused since the last report.",
                        refusal.Scope,
                        (int)refusal.RetryAfter.TotalSeconds,
                        leftOut
                    );
                }

                return refusal;
            }

            foreach (var budget in budgets)
            {
                if (!_windows.TryGetValue(budget.Key, out var window) || window.ResetsAt <= now)
                {
                    window = new Window(now + _options.Window);
                    _windows[budget.Key] = window;
                }

                window.Count++;
            }

            return null;
        }
    }

    /// <summary>A window that is over is of no use to anyone: they are dropped now and then, so a flood of addresses cannot fill the memory.</summary>
    void Sweep(DateTimeOffset now)
    {
        if (now < _nextSweep)
        {
            return;
        }

        _nextSweep = now + SWEEP_EVERY;
        foreach (var expired in _windows.Where(x => x.Value.ResetsAt <= now).Select(x => x.Key).ToList())
        {
            _windows.Remove(expired);
        }
    }

    string AddressOf(string email)
    {
        return _normalizer.NormalizeEmail(email) ?? string.Empty;
    }

    sealed class Window
    {
        public Window(DateTimeOffset resetsAt)
        {
            ResetsAt = resetsAt;
        }

        public DateTimeOffset ResetsAt { get; }
        public int Count { get; set; }
    }

    sealed class Budget
    {
        public Budget(string scope, string key, int limit)
        {
            Scope = scope;
            Key = key;
            Limit = limit;
        }

        public string Scope { get; }
        public string Key { get; }
        public int Limit { get; }
    }
}
