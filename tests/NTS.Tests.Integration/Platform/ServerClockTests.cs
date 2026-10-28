using System.Net;
using NoTiming.Ui.Features.Core;
using NTS.Tests.Integration.Harness;

namespace NTS.Tests.Integration;

/// <summary>
/// The time of the server as the Ui knows it (#646): what the Api says it is on every answer, against the clock of the device,
/// so that a request for a Representation or an Inspection is placed in a Phase by the clock of the server.
/// </summary>
public sealed class ServerClockTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset DEVICE = new(2031, 5, 3, 7, 0, 0, TimeSpan.Zero);

    readonly MongoFixture _mongo;

    public ServerClockTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_server_is_as_far_ahead_of_the_device_as_the_time_it_says_in_its_answer_is()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var json = JsonApiClients.WithCookie(api, new JsonApiClients.Cookie(null), out var requests);
        requests.Answer = _ => Said(DEVICE.AddMinutes(5));
        var clock = new ServerClock(json, new Fixed(DEVICE));

        var before = clock.Now;
        await clock.Synchronise();

        Assert.Equal(DEVICE, before);
        Assert.Equal(DEVICE.AddMinutes(5), clock.Now);
    }

    [Fact]
    public async Task The_time_a_request_took_does_not_count_in_the_difference()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var json = JsonApiClients.WithCookie(api, new JsonApiClients.Cookie(null), out var requests);
        var device = new Moving(DEVICE);
        requests.Answer = _ =>
        {
            device.Advance(TimeSpan.FromSeconds(10)); // the request is out for ten seconds
            return Said(DEVICE.AddSeconds(5)); // and the server said it was five seconds past the moment it was asked
        };
        var clock = new ServerClock(json, device);

        await clock.Synchronise();

        // the answer came in the middle of the ten seconds, so the server was on the same time as the device then
        Assert.Equal(device.GetUtcNow(), clock.Now);
    }

    [Fact]
    public async Task A_server_that_says_nothing_or_cannot_be_reached_leaves_the_clock_of_the_device_as_it_is()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var json = JsonApiClients.WithCookie(api, new JsonApiClients.Cookie(null), out var requests);
        var clock = new ServerClock(json, new Fixed(DEVICE));

        requests.Answer = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized); // an answer with no time in it
        await clock.Synchronise();
        var silent = clock.Now;
        requests.Answer = _ => throw new HttpRequestException("There is no server.");
        await clock.Synchronise();

        Assert.Equal(DEVICE, silent);
        Assert.Equal(DEVICE, clock.Now);
    }

    static HttpResponseMessage Said(DateTimeOffset time)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        response.Headers.Date = time;
        return response;
    }

    sealed class Fixed : TimeProvider
    {
        readonly DateTimeOffset _now;

        public Fixed(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
    }

    sealed class Moving : TimeProvider
    {
        DateTimeOffset _now;

        public Moving(DateTimeOffset now)
        {
            _now = now;
        }

        public void Advance(TimeSpan by)
        {
            _now += by;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
    }
}
