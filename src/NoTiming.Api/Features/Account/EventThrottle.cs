namespace NoTiming.Api.Features.Account;

/// <summary>
/// Lets an event that anyone can cause as often as they like be logged once a minute, with the number of times it was
/// left out since the last (#601). A flood of refused requests, of bots that filled the hidden field or of addresses
/// that may not register would otherwise write a line for each, and the log is the one thing an attacker should not be
/// able to fill. Each key is reported on its own, and there are only as many keys as there are kinds of event.
/// </summary>
internal sealed class EventThrottle
{
    static readonly TimeSpan INTERVAL = TimeSpan.FromMinutes(1);

    readonly object _lock = new();
    readonly Dictionary<string, Entry> _entries = new();
    readonly TimeProvider _time;

    public EventThrottle(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>
    /// Whether the event may be logged now. When it may, <paramref name="leftOut"/> is how many were not logged since
    /// the last one that was; when it may not, the occurrence is counted for the next.
    /// </summary>
    public bool ShouldLog(string key, out int leftOut)
    {
        lock (_lock)
        {
            var now = _time.GetUtcNow();
            if (_entries.TryGetValue(key, out var entry) && now - entry.LoggedAt < INTERVAL)
            {
                entry.LeftOut++;
                leftOut = 0;
                return false;
            }

            leftOut = entry?.LeftOut ?? 0;
            _entries[key] = new Entry(now);
            return true;
        }
    }

    sealed class Entry
    {
        public Entry(DateTimeOffset loggedAt)
        {
            LoggedAt = loggedAt;
        }

        public DateTimeOffset LoggedAt { get; }
        public int LeftOut { get; set; }
    }
}
