using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NC.Server.Authentication.Store;
using NC.Storage.Mongo;

namespace NC.Server.Authentication.RateLimits;

public sealed class RateCounterStore
{
    readonly IMongoCollection<RateCounterDocument> _counters;

    public RateCounterStore(IMongoContext context, IOptions<AuthenticationSettings> options)
    {
        var settings = options.Value;
        _counters = context
            .Client.GetDatabase(settings.Database)
            .GetCollection<RateCounterDocument>(settings.RateCountersCollection);
    }

    public async Task<int> CountAsync(
        string scope,
        string key,
        DateTimeOffset hourStart,
        CancellationToken cancellationToken = default
    )
    {
        var counter = await _counters
            .Find(x => x.Key == Name(scope, key, hourStart))
            .FirstOrDefaultAsync(cancellationToken);
        return counter?.Count ?? 0;
    }

    public Task IncrementAsync(
        string scope,
        string key,
        DateTimeOffset hourStart,
        CancellationToken cancellationToken = default
    )
    {
        return _counters.UpdateOneAsync(
            x => x.Key == Name(scope, key, hourStart),
            Builders<RateCounterDocument>.Update.Combine(
                Builders<RateCounterDocument>.Update.Inc(x => x.Count, 1),
                Builders<RateCounterDocument>.Update.SetOnInsert(x => x.ExpiresAt, hourStart.AddHours(2).UtcDateTime)
            ),
            new UpdateOptions { IsUpsert = true },
            cancellationToken
        );
    }

    static string Name(string scope, string key, DateTimeOffset hourStart)
    {
        return $"{scope}|{key}|{hourStart.UtcDateTime:yyyyMMddHH}";
    }
}
