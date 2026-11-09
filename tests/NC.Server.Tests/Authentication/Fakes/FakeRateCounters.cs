using NC.Server.Authentication.RateLimits;

namespace NC.Server.Tests.Authentication.Fakes;

/// <summary>Counts nothing, so every send is allowed.</summary>
internal sealed class FakeRateCounters : IRateCounterRepository
{
    public Task<int> Count(
        string scope,
        string key,
        DateTimeOffset hourStart,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(0);
    }

    public Task Increment(
        string scope,
        string key,
        DateTimeOffset hourStart,
        CancellationToken cancellationToken = default
    )
    {
        return Task.CompletedTask;
    }
}
