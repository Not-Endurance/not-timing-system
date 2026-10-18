using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace NTS.Tools.IdentityMigration;

/// <summary>
/// Reads a collection a page at a time and goes on when the server throttles it (#636). Cosmos DB for MongoDB answers a request that
/// is over its rate with error 16500 and says how long to wait (<c>RetryAfterMs</c>); Atlas retries what it can inside the driver.
/// A page is asked for again after the wait, any other error is the caller's, and a read that stays throttled gives up with the last
/// error. The pages are in the order of the ids, which every type of id has, so a page does not overlap the next.
/// </summary>
public static partial class ThrottledReads
{
    const int THROTTLED = 16500;
    const int TOO_MANY_REQUESTS = 429;
    const int ATTEMPTS = 8;
    static readonly TimeSpan FirstBackoff = TimeSpan.FromMilliseconds(100);
    static readonly TimeSpan LongestWait = TimeSpan.FromMinutes(1);
    public const int PAGE_SIZE = 100;

    /// <summary>Every document of the collection, a page at a time, so that what is read can be dropped before the next page is.</summary>
    public static async IAsyncEnumerable<BsonDocument> Documents(
        IMongoCollection<BsonDocument> collection,
        int pageSize = PAGE_SIZE,
        Func<TimeSpan, Task>? wait = null,
        [EnumeratorCancellation] CancellationToken cancellation = default
    )
    {
        var read = 0;
        while (true)
        {
            var skip = read;
            var page = await Retried(
                () =>
                    collection
                        .Find(FilterDefinition<BsonDocument>.Empty)
                        .Sort(Builders<BsonDocument>.Sort.Ascending("_id"))
                        .Skip(skip)
                        .Limit(pageSize)
                        .ToListAsync(cancellation),
                wait ?? (delay => Task.Delay(delay, cancellation))
            );
            foreach (var document in page)
            {
                yield return document;
            }

            read += page.Count;
            if (page.Count < pageSize)
            {
                yield break;
            }
        }
    }

    /// <summary>Runs the read, and again after the wait the server asks for while it throttles it, up to <paramref name="attempts"/> times.</summary>
    public static async Task<T> Retried<T>(Func<Task<T>> read, Func<TimeSpan, Task> wait, int attempts = ATTEMPTS)
    {
        var backoff = FirstBackoff;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await read();
            }
            catch (MongoCommandException error) when (attempt < attempts && IsThrottled(error))
            {
                var asked = AskedToWait(error);
                await wait(Min(asked ?? backoff, LongestWait));
                backoff *= 2;
            }
        }
    }

    static bool IsThrottled(MongoCommandException error)
    {
        return error.Code is THROTTLED or TOO_MANY_REQUESTS
            || error.Message.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase)
            || RetryAfter().IsMatch(error.Message);
    }

    static TimeSpan? AskedToWait(MongoCommandException error)
    {
        var match = RetryAfter().Match(error.Message);
        return match.Success && double.TryParse(match.Groups[1].Value, out var milliseconds)
            ? TimeSpan.FromMilliseconds(milliseconds)
            : null;
    }

    static TimeSpan Min(TimeSpan one, TimeSpan another)
    {
        return one < another ? one : another;
    }

    [GeneratedRegex(@"RetryAfterMs\s*=\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex RetryAfter();
}
