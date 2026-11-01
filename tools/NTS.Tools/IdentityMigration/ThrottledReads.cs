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

    /// <summary>
    /// Runs the request, a read or a write, and again after the wait the server asks for while it throttles it, up to
    /// <paramref name="attempts"/> times. The server says so with a command error, and for a write with a write error. The wait is
    /// the clock's delay unless a test gives its own.
    /// </summary>
    public static async Task<T> Retried<T>(
        Func<Task<T>> request,
        Func<TimeSpan, Task>? wait = null,
        int attempts = ATTEMPTS
    )
    {
        wait ??= delay => Task.Delay(delay);
        var backoff = FirstBackoff;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await request();
            }
            catch (Exception error) when (attempt < attempts && IsThrottled(error))
            {
                await wait(Min(AskedToWait(error.Message) ?? backoff, LongestWait));
                backoff *= 2;
            }
        }
    }

    /// <summary>The same for a request that answers nothing.</summary>
    public static async Task Retried(Func<Task> request, Func<TimeSpan, Task>? wait = null, int attempts = ATTEMPTS)
    {
        await Retried(
            async () =>
            {
                await request();
                return true;
            },
            wait,
            attempts
        );
    }

    static bool IsThrottled(Exception error)
    {
        return error switch
        {
            MongoCommandException command => IsThrottled(command.Code, command.Message),
            MongoWriteException write => IsThrottled(write.WriteError.Code, write.Message),
            _ => false,
        };
    }

    static bool IsThrottled(int code, string message)
    {
        return code is THROTTLED or TOO_MANY_REQUESTS
            || message.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase)
            || RetryAfter().IsMatch(message);
    }

    static TimeSpan? AskedToWait(string message)
    {
        var match = RetryAfter().Match(message);
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
