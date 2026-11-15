using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NC.Server.Authentication.RateLimits;
using NC.Server.Authentication.Settings;
using NC.Storage.Mongo;

namespace NC.Server.Authentication.Stores.MongoDB.Repositories;

internal sealed class RateCounterRepository : IRateCounterRepository
{
    readonly IMongoCollection<RateCounterDocument> _counters;

    public RateCounterRepository(IMongoContext context, IOptions<AuthenticationSettings> options)
    {
        var settings = options.Value;
        _counters = context
            .Client.GetDatabase(settings.Database)
            .GetCollection<RateCounterDocument>(settings.RateCountersCollection);
    }

    public async Task<int> Count(
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

    public Task Increment(
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
                Builders<RateCounterDocument>.Update.SetOnInsert(x => x.ExpiresAtUtc, hourStart.AddHours(2).UtcDateTime)
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
