namespace NoTiming.Api.Features.Account;

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

/// <summary>One budget a request draws on: a name that says which, the key it is counted under, and how many fit in a window.</summary>
internal sealed class RateBudget
{
    public RateBudget(string scope, string key, int limit)
    {
        Scope = scope;
        Key = key;
        Limit = limit;
    }

    public string Scope { get; }
    public string Key { get; }
    public int Limit { get; }
}

/// <summary>
/// The counting behind the limits of the Api (#601, #643): windows of a fixed length, in memory and per host, one for each
/// budget key, that open with the first request that draws on them. A request draws on several budgets at once and is
/// refused, at no cost to any of them, when one is spent; otherwise it costs one of each. A window that is over is of no
/// use to anyone and is dropped now and then, so a flood of keys cannot fill the memory.
/// </summary>
internal sealed class RateLedger
{
    static readonly TimeSpan SWEEP_EVERY = TimeSpan.FromMinutes(1);

    readonly object _lock = new();
    readonly Dictionary<string, Window> _windows = new();
    readonly TimeProvider _time;
    DateTimeOffset _nextSweep;

    public RateLedger(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>Takes the cost of a request from its budgets; null when there was room, the budget that was spent when there was not.</summary>
    public RateLimitRefusal? TryTake(IReadOnlyList<RateBudget> budgets, TimeSpan window)
    {
        lock (_lock)
        {
            var now = _time.GetUtcNow();
            Sweep(now);

            RateLimitRefusal? refusal = null;
            foreach (var budget in budgets)
            {
                if (!_windows.TryGetValue(budget.Key, out var open) || open.ResetsAt <= now)
                {
                    continue;
                }

                if (open.Count >= budget.Limit && (refusal is null || open.ResetsAt - now > refusal.RetryAfter))
                {
                    refusal = new RateLimitRefusal(budget.Scope, open.ResetsAt - now);
                }
            }

            if (refusal != null)
            {
                return refusal;
            }

            foreach (var budget in budgets)
            {
                if (!_windows.TryGetValue(budget.Key, out var open) || open.ResetsAt <= now)
                {
                    open = new Window(now + window);
                    _windows[budget.Key] = open;
                }

                open.Count++;
            }

            return null;
        }
    }

    /// <summary>Gives back what <see cref="TryTake"/> took from budgets whose windows are still open.</summary>
    public void Give(IReadOnlyList<RateBudget> budgets)
    {
        lock (_lock)
        {
            var now = _time.GetUtcNow();
            foreach (var budget in budgets)
            {
                if (_windows.TryGetValue(budget.Key, out var open) && open.ResetsAt > now && open.Count > 0)
                {
                    open.Count--;
                }
            }
        }
    }

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

    sealed class Window
    {
        public Window(DateTimeOffset resetsAt)
        {
            ResetsAt = resetsAt;
        }

        public DateTimeOffset ResetsAt { get; }
        public int Count { get; set; }
    }
}
