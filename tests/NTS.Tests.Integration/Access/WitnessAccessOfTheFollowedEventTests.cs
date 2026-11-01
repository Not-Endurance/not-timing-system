using Not.Application.RPC;
using NoTiming.Ui.Features.Access;
using NoTiming.Ui.Features.Account;
using NTS.Contracts.Features.Access;
using NTS.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Tests.Integration.Harness.Drivers;
using NTS.Tests.Integration.Harness;
using static NTS.Tests.Integration.Harness.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// What the Witness says of a person before there is an Event to say it about (#631): the level of a person who follows no
/// Event is not the level for the Event that the connection names next. The viewed Event is opened as soon as the connection
/// names the Event, which can be while the connection is still handing the Event to the services that follow it, and it loads
/// the access it wraps then. It must be told what the Api says of the person and that Event, and not that the person is only
/// registered, which sends an Official from the Snapshot page to the Performance page. Over the real Api in this process.
/// </summary>
public sealed class WitnessAccessOfTheFollowedEventTests : IClassFixture<SharedApiFixture>
{
    readonly SharedApiFixture _shared;

    public WitnessAccessOfTheFollowedEventTests(SharedApiFixture shared)
    {
        _shared = shared;
    }

    [Fact]
    public async Task The_access_of_a_person_who_followed_no_Event_is_made_again_when_it_is_loaded_for_the_Event_that_is_followed_next_and_then_it_is_made_once()
    {
        var api = _shared.Api;
        using var client = api.CreateClient();
        var (eventId, official) = await SceneAsync(api, client);
        var json = JsonApiClients.Of(api, official, out var asked);
        using var account = new AccountSession(json);
        var followed = new FollowedEvent();
        using var access = new WitnessAccessContext(followed, account, json);

        await access.Load(); // the page is loaded and no Event is followed

        Assert.Equal(WitnessSays.Registered, WitnessSays.Of(access));
        Assert.Equal(0, CapabilityRequests(asked)); // there is no Event to ask about

        followed.Follow(eventId); // the connection names the Event, and the services that follow it have not been told yet
        await access.Load(); // the viewed Event loads what it wraps

        Assert.Equal(WitnessSays.Official, WitnessSays.Of(access));
        Assert.Equal(1, CapabilityRequests(asked));

        await access.Load();
        await access.Load();

        Assert.Equal(1, CapabilityRequests(asked)); // what the Api said of the person and the Event is not asked again by loading
    }

    [Fact]
    public async Task The_access_for_an_Event_that_is_followed_from_the_start_is_made_once()
    {
        var api = _shared.Api;
        using var client = api.CreateClient();
        var (eventId, official) = await SceneAsync(api, client);
        var json = JsonApiClients.Of(api, official, out var asked);
        using var account = new AccountSession(json);
        var followed = new FollowedEvent();
        followed.Follow(eventId);
        using var access = new WitnessAccessContext(followed, account, json);

        await access.Load();
        await access.Load();

        Assert.Equal(WitnessSays.Official, WitnessSays.Of(access));
        Assert.Equal(1, CapabilityRequests(asked));
    }

    static int CapabilityRequests(JsonApiClients.Requests asked)
    {
        lock (asked.Asked)
        {
            return asked.Asked.Count(x => x.Contains("/capabilities"));
        }
    }

    async Task<(Guid EventId, TenancySeed.Person Official)> SceneAsync(ApiFactory api, HttpClient client)
    {
        var tenant = await TenantAsync(_shared.MongoConnectionString);
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _shared.MongoConnectionString,
            tenant,
            consoleOperator.Id,
            DateTimeOffset.UtcNow
        );
        var official = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        await EventSeed.GrantAsync(
            _shared.MongoConnectionString,
            tenant,
            eventId,
            "Official",
            "Steward",
            official.Email,
            official.Id
        );
        return (eventId, official);
    }

    /// <summary>The Event the Ui follows, if it follows one.</summary>
    sealed class FollowedEvent : INtsSocketContext
    {
        public bool IsConnected => Event != null;

        public SocketConnectionStatus Status =>
            IsConnected ? SocketConnectionStatus.Connected : SocketConnectionStatus.Disconnected;

        public EventInformation? Event { get; private set; }

        public void Follow(Guid eventId)
        {
            Event = IntegrationPayloadFactory.EventInformation(eventId);
        }
    }
}
