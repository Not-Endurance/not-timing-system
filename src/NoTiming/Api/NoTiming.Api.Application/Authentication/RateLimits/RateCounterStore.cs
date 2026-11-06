using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using NC.Storage.Mongo;
using NoTiming.Api.Application.Authentication.Store;

namespace NoTiming.Api.Application.Authentication.RateLimits;

/// <summary>A count of sends in one scope and one hour bucket. Expires two hours after the bucket starts.</summary>
[BsonIgnoreExtraElements]
public sealed class RateCounterDocument
{
    /// <summary>scope|key|yyyyMMddHH</summary>
    [BsonId]
    public string Key { get; set; } = string.Empty;

    public int Count { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpiresAt { get; set; }
}

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
