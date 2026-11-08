namespace NC.Server.Authentication.RateLimits;

/// <summary>A count of sends in one scope and one hour bucket. Expires two hours after the bucket starts.</summary>
internal sealed class RateCounterDocument
{
    /// <summary>scope|key|yyyyMMddHH</summary>
    public string Key { get; set; } = string.Empty;

    public int Count { get; set; }

    /// <summary>The TTL index deletes the counter once this has passed.</summary>
    public DateTime ExpiresAtUtc { get; set; }
}
