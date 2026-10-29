using System.Net;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using NTS.Contracts;
using NTS.Contracts.Live;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// What a viewer is told when a Snapshot is recorded (#644, ADR-0013): the Api sends <c>ParticipationChanged</c> with the ids
/// of the Event and of the Participation, and nothing else, to whoever joined the Event's group, signed in or not, and each
/// write that was stored is one notification. Nobody has to be connected for the time to be recorded: the Snapshot of an
/// Official is a request to the Api, whether or not a Console, a viewer or anything else is there. The real hub of the real
/// host, and a viewer that has joined without signing in.
/// </summary>
public sealed class SnapshotNotificationTests : IClassFixture<ApiHostFixture>
{
    static readonly TimeSpan PATIENCE = TimeSpan.FromSeconds(15);
    static readonly DateTimeOffset START = new(2030, 5, 21, 8, 0, 0, TimeSpan.Zero);

    readonly ApiHostFixture _host;

    public SnapshotNotificationTests(ApiHostFixture host)
    {
        _host = host;
    }

    [Fact]
    public async Task A_viewer_that_joined_the_Event_without_signing_in_is_told_which_Participation_changed_after_each_write_and_nothing_else()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenantAsync(mongo);
        using var client = _host.Api.CreateClient();
        var official = await SignedInAsync(_host.Api, client, mongo, tenant);
        var eventId = await EventSeed.LiveAsync(mongo, tenant, null, DateTimeOffset.UtcNow);
        var participation = IntegrationPayloadFactory.ActiveParticipation(eventId, 1, Guid.NewGuid(), startTime: START);
        await EventSeed.ParticipationAsync(mongo, tenant, participation);
        await EventSeed.GrantAsync(mongo, tenant, eventId, "Official", "Steward", official.Email, official.Id);
        await using var viewer = new HubConnectionBuilder()
            .WithUrl(new Uri(_host.BaseAddress, $"{ApplicationConstants.LIVE_HUB}?connectionGroup={eventId}"))
            .AddNewtonsoftJsonProtocol()
            .Build();
        var told = new List<(Guid EventId, Guid ParticipationId)>();
        viewer.On<Guid, Guid>(
            nameof(ILiveClientProcedures.ParticipationChanged),
            (eventId, participationId) =>
            {
                lock (told)
                {
                    told.Add((eventId, participationId));
                }
            }
        );
        await viewer.StartAsync(); // no Console is connected to the Event: there is nothing else to be

        var arrived = await Send(official, eventId, "Arrive", START.AddMinutes(30));
        await WaitUntil(() => told.Count == 1);
        var presented = await Send(official, eventId, "Present", START.AddMinutes(40));
        await WaitUntil(() => told.Count == 2);
        var rejected = await Send(official, eventId, "Arrive", START.AddMinutes(50)); // recorded, as a rejected event
        await WaitUntil(() => told.Count == 3);
        await Task.Delay(300);

        Assert.Equal(HttpStatusCode.Created, arrived.StatusCode);
        Assert.Equal(HttpStatusCode.Created, presented.StatusCode);
        Assert.Equal(HttpStatusCode.Created, rejected.StatusCode);
        Assert.Equal(
            [(eventId, participation.Id), (eventId, participation.Id), (eventId, participation.Id)],
            told.ToArray()
        );
        var stored = (await RegistrySeed.StoredAsync(mongo, "event_participations", participation.Id))!;
        Assert.Equal(3, stored["Phases"][0]["Events"].AsBsonArray.Count);
    }

    static Task<HttpResponseMessage> Send(Person official, Guid eventId, string kind, DateTimeOffset time)
    {
        return official.Page.WriteAsync(
            HttpMethod.Post,
            "/api/snapshots",
            "snapshots",
            new
            {
                eventId,
                number = 1,
                kind,
                time,
            },
            id: Guid.NewGuid().ToString()
        );
    }

    static async Task WaitUntil(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(PATIENCE);
        while (!condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }
}
