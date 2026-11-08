namespace NC.Server.Authentication.RateLimits;

/// <summary>Counts the sends in one scope, for one key, in one hour.</summary>
public interface IRateCounterRepository
{
    Task<int> Count(string scope, string key, DateTimeOffset hourStart, CancellationToken cancellationToken = default);

    Task Increment(string scope, string key, DateTimeOffset hourStart, CancellationToken cancellationToken = default);
}
