using System.Net;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;
using NTS.Tools.IdentityMigration;

namespace NTS.Tests.Unit.Temporary;

/// <summary>#636: reads that go on when Cosmos DB for MongoDB says the request rate is too large, and stop for anything else.</summary>
public sealed class ThrottledReadsTests
{
    [Fact]
    public async Task A_read_that_works_is_not_waited_for()
    {
        var waits = new List<TimeSpan>();

        var result = await ThrottledReads.Retried(() => Task.FromResult(42), Record(waits));

        Assert.Equal(42, result);
        Assert.Empty(waits);
    }

    [Fact]
    public async Task A_throttled_read_waits_as_long_as_the_server_asks_and_is_asked_again()
    {
        var waits = new List<TimeSpan>();
        var calls = 0;

        var result = await ThrottledReads.Retried(
            () =>
            {
                calls++;
                return calls < 3
                    ? throw Throttled(16500, "Request rate is large. RetryAfterMs=250")
                    : Task.FromResult("page");
            },
            Record(waits)
        );

        Assert.Equal("page", result);
        Assert.Equal(3, calls);
        Assert.Equal([TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250)], waits);
    }

    [Theory]
    [InlineData(16500, "Request rate is large.")]
    [InlineData(429, "Too many requests")]
    [InlineData(1, "TooManyRequests: slow down")]
    [InlineData(1, "Error=16500, RetryAfterMs=20")]
    [InlineData(1, "retryafterms = 20")]
    public async Task Each_way_the_server_says_it_is_throttling_is_waited_out(int code, string message)
    {
        var waits = new List<TimeSpan>();
        var calls = 0;

        await ThrottledReads.Retried(
            () =>
            {
                calls++;
                return calls < 2 ? throw Throttled(code, message) : Task.FromResult(1);
            },
            Record(waits)
        );

        Assert.Single(waits);
    }

    [Fact]
    public async Task A_throttle_that_does_not_say_how_long_is_waited_out_for_longer_each_time()
    {
        var waits = new List<TimeSpan>();
        var calls = 0;

        await ThrottledReads.Retried(
            () =>
            {
                calls++;
                return calls < 4 ? throw Throttled(16500, "Request rate is large.") : Task.FromResult(1);
            },
            Record(waits)
        );

        Assert.Equal(3, waits.Count);
        Assert.True(waits[0] > TimeSpan.Zero);
        Assert.True(waits[1] > waits[0]);
        Assert.True(waits[2] > waits[1]);
    }

    [Fact]
    public async Task A_read_that_fails_for_another_reason_is_not_retried()
    {
        var waits = new List<TimeSpan>();
        var calls = 0;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                ThrottledReads.Retried<int>(
                    () =>
                    {
                        calls++;
                        throw new InvalidOperationException("broken");
                    },
                    Record(waits)
                )
        );

        Assert.Equal("broken", error.Message);
        Assert.Equal(1, calls);
        Assert.Empty(waits);
    }

    [Fact]
    public async Task A_command_error_that_is_not_a_throttle_is_not_retried()
    {
        var calls = 0;

        await Assert.ThrowsAsync<MongoCommandException>(
            () =>
                ThrottledReads.Retried<int>(
                    () =>
                    {
                        calls++;
                        throw Throttled(13, "Unauthorized");
                    },
                    Record([])
                )
        );

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task A_read_that_stays_throttled_gives_up_with_the_last_error()
    {
        var waits = new List<TimeSpan>();
        var calls = 0;

        var error = await Assert.ThrowsAsync<MongoCommandException>(
            () =>
                ThrottledReads.Retried<int>(
                    () =>
                    {
                        calls++;
                        throw Throttled(16500, "Request rate is large. RetryAfterMs=5");
                    },
                    Record(waits),
                    attempts: 4
                )
        );

        Assert.Equal(16500, error.Code);
        Assert.Equal(4, calls);
        Assert.Equal(3, waits.Count);
    }

    [Fact]
    public async Task The_wait_the_server_asks_for_is_kept_within_a_minute()
    {
        var waits = new List<TimeSpan>();
        var calls = 0;

        await ThrottledReads.Retried(
            () =>
            {
                calls++;
                return calls < 2 ? throw Throttled(16500, "RetryAfterMs=9999999") : Task.FromResult(1);
            },
            Record(waits)
        );

        Assert.Equal(TimeSpan.FromMinutes(1), Assert.Single(waits));
    }

    static Func<TimeSpan, Task> Record(List<TimeSpan> waits)
    {
        return wait =>
        {
            waits.Add(wait);
            return Task.CompletedTask;
        };
    }

    static MongoCommandException Throttled(int code, string message)
    {
        var connection = new ConnectionId(new ServerId(new ClusterId(1), new DnsEndPoint("localhost", 27017)));
        return new MongoCommandException(
            connection,
            message,
            new BsonDocument("find", "athletes"),
            new BsonDocument
            {
                ["ok"] = 0,
                ["code"] = code,
                ["errmsg"] = message,
            }
        );
    }
}
