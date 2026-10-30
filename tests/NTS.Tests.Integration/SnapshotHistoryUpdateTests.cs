using MongoDB.Bson;
using Not.Domain.Exceptions;
using Not.Notify;
using NoTiming.Api.Features.Reference;
using NoTiming.Ui.Features.Account;
using NoTiming.Ui.Features.Core.Dashboard;
using NoTiming.Ui.Features.Core.Participations;
using NoTiming.Ui.Features.Sessions;
using NoTiming.Ui.Storage.Core.Repositories;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Events;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Localization.NtsStrings;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The correction of the time of a Snapshot that was sent (#618, ADR-0013), through the service that the Snapshot page calls,
/// over the real Api in this process: the Official sends a time, corrects it from the History, and the Participation, the
/// entry and the history that the host keeps all say the new time, also on a page that is opened again; a time that breaks
/// the order of the Phase is recorded and not taken and the entry is as it was; a person who may not write is refused before
/// the Api is asked.
/// </summary>
public sealed class SnapshotHistoryUpdateTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset START = new(2030, 5, 21, 8, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset ARRIVE = START.AddMinutes(30);

    readonly MongoFixture _mongo;

    public SnapshotHistoryUpdateTests(MongoFixture mongo)
    {
        ApiMongo.Configure();
        _mongo = mongo;
    }

    [Fact]
    public async Task The_Official_corrects_a_time_it_sent_and_the_Participation_the_entry_and_the_kept_history_say_the_new_one_also_when_the_page_is_opened_again()
    {
        var (api, official, eventId, ids) = await SceneAsync(1);
        await using var host = api;
        var kept = new MemoryUnansweredSnapshots();
        await using var page = await Page.OpenAsync(api, official, eventId, kept);
        await SendAsync(page, 1, ARRIVE);
        var group = Assert.Single(page.Service.History);
        var entry = Assert.Single(group.Entries);
        var corrected = new Timestamp(ARRIVE.AddMinutes(5));

        await page.Service.UpdateTime(page.View, group, entry, corrected);

        Assert.Equal(corrected, entry.Timestamp); // the entry shows it
        Assert.Equal(["Arrived", "ArriveUpdated"], (await EventsAsync(ids[0])).Select(x => x["Kind"].AsString));
        await page.Store.Refresh(ids[0]);
        Assert.Equal(ARRIVE.AddMinutes(5), page.Store.Find(ids[0])!.Phases.First().ArriveTime!.ToDateTimeOffset()); // and so does the Phase
        await using var reopened = await Page.OpenAsync(api, official, eventId, kept);
        var again = Assert.Single(reopened.Service.History);
        var againEntry = Assert.Single(again.Entries);
        Assert.Equal(group.Id, again.Id); // the kept history has the id and the time of the correction
        Assert.Equal(ARRIVE.AddMinutes(5), againEntry.Timestamp!.ToDateTimeOffset());
        Assert.True(reopened.Service.CanUpdate(again, againEntry));
        await reopened.Service.UpdateTime(reopened.View, again, againEntry, new Timestamp(ARRIVE.AddMinutes(8))); // and it is corrected again by the id that was kept
        await page.Store.Refresh(ids[0]);
        Assert.Equal(ARRIVE.AddMinutes(8), page.Store.Find(ids[0])!.Phases.First().ArriveTime!.ToDateTimeOffset());
        Assert.Equal(
            ["Arrived", "ArriveUpdated", "ArriveUpdated"],
            (await EventsAsync(ids[0])).Select(x => x["Kind"].AsString)
        );
    }

    [Fact]
    public async Task A_time_that_breaks_the_order_of_the_Phase_is_recorded_and_not_taken_and_the_entry_and_the_history_are_as_they_were()
    {
        var (api, official, eventId, ids) = await SceneAsync(1);
        await using var host = api;
        var kept = new MemoryUnansweredSnapshots();
        await using var page = await Page.OpenAsync(api, official, eventId, kept);
        await SendAsync(page, 1, ARRIVE);
        var group = Assert.Single(page.Service.History);
        var entry = Assert.Single(group.Entries);

        var refused = await Assert.ThrowsAsync<DomainException>(
            () => page.Service.UpdateTime(page.View, group, entry, new Timestamp(START.AddMinutes(-10))) // before the Start
        );

        Assert.Equal(string.Format(Time_not_taken_string, "Rejected Invalid Time"), refused.Message);
        Assert.Equal(ARRIVE, entry.Timestamp!.ToDateTimeOffset());
        Assert.Equal(["Arrived", "ArriveUpdated"], (await EventsAsync(ids[0])).Select(x => x["Kind"].AsString)); // it is an event of its own
        Assert.Equal(
            ARRIVE,
            Assert
                .Single(Assert.Single((await page.Sessions.GetCurrent())!.GetSnapshotHistory()).Entries)
                .Timestamp!.ToDateTimeOffset()
        );
        Assert.True(page.Service.CanUpdate(group, entry)); // and the person may try another time
    }

    [Fact]
    public async Task A_person_who_may_not_write_cannot_correct_a_time_and_the_Api_is_not_asked()
    {
        var (api, official, eventId, _) = await SceneAsync(1);
        await using var host = api;
        await using var writer = await Page.OpenAsync(api, official, eventId, new MemoryUnansweredSnapshots());
        await SendAsync(writer, 1, ARRIVE);
        var group = Assert.Single(writer.Service.History);
        var entry = Assert.Single(group.Entries);
        await using var reader = await Page.OpenAsync(
            api,
            official,
            eventId,
            new MemoryUnansweredSnapshots(),
            canWrite: false
        );

        await Assert.ThrowsAsync<DomainException>(
            () => reader.Service.UpdateTime(reader.View, group, entry, new Timestamp(ARRIVE.AddMinutes(1)))
        );

        Assert.DoesNotContain(
            reader.Requests.Asked,
            x => x.StartsWith("PATCH /api/snapshots", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            writer.Requests.Asked,
            x => x.StartsWith("PATCH /api/snapshots", StringComparison.Ordinal)
        );
    }

    static async Task SendAsync(Page page, int number, DateTimeOffset time)
    {
        page.Service.SelectForSnapshot(page.Service.Participations.Single(x => x.Combination.Number == number));
        page.Service.UpdateTimestamp(page.Service.Snapshots.Single(), new Timestamp(time));
        Assert.True(await page.Service.Publish(page.View, SnapshotType.Arrive));
    }

    async Task<(ApiFactory Api, Person Official, Guid EventId, IReadOnlyList<Guid> Participations)> SceneAsync(
        int participations
    )
    {
        var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            consoleOperator.Id,
            DateTimeOffset.UtcNow
        );
        var official = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            eventId,
            "Official",
            "Steward",
            official.Email,
            official.Id
        );
        var ids = new List<Guid>();
        for (var number = 1; number <= participations; number++)
        {
            var participation = IntegrationPayloadFactory.ActiveParticipation(
                eventId,
                number,
                Guid.NewGuid(),
                startTime: START
            );
            await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation);
            ids.Add(participation.Id);
        }

        return (api, official, eventId, ids);
    }

    async Task<List<BsonDocument>> EventsAsync(Guid participation)
    {
        var stored = (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", participation))!;
        return [.. stored["Phases"][0]["Events"].AsBsonArray.Select(x => x.AsBsonDocument)];
    }

    /// <summary>The Snapshot page of a person: the service it calls, over the stores and the clients of the Ui, reaching the Api in this process.</summary>
    sealed class Page : IAsyncDisposable
    {
        public static async Task<Page> OpenAsync(
            ApiFactory api,
            Person person,
            Guid eventId,
            MemoryUnansweredSnapshots kept,
            bool canWrite = true
        )
        {
            var json = JsonApiClients.Of(api, person, out var requests);
            var selected = new SelectedEvent(eventId);
            var repository = new ParticipationEventScopedApiRepository(
                json,
                new EventScopeFactory<Participation>(selected)
            );
            var store = new ParticipationStore(repository);
            var account = new AccountSession(json);
            var sessions = new WitnessUserSessionService(account, json);
            await sessions.SetEventId(eventId);
            var service = new SnapshotService(
                selected,
                store,
                sessions,
                new SnapshotApiPublisher(json),
                account,
                kept,
                new SnapshotResendTimer(),
                new Quiet()
            );
            await service.Load();
            return new Page(service, store, sessions, new StubViewedEvent(store, selected.Event!, canWrite), requests);
        }

        Page(
            SnapshotService service,
            ParticipationStore store,
            WitnessUserSessionService sessions,
            StubViewedEvent view,
            JsonApiClients.Requests requests
        )
        {
            Service = service;
            Store = store;
            Sessions = sessions;
            View = view;
            Requests = requests;
        }

        public SnapshotService Service { get; }
        public ParticipationStore Store { get; }
        public WitnessUserSessionService Sessions { get; }
        public StubViewedEvent View { get; }
        public JsonApiClients.Requests Requests { get; }

        public ValueTask DisposeAsync()
        {
            Service.Dispose();
            Store.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>A notifier that tells nobody: what the person would be told is what the service throws.</summary>
    sealed class Quiet : INotifier
    {
        public void Inform(string message) { }

        public void Success(string message) { }

        public void Warn(string message) { }

        public void Warn(IEnumerable<string> messages) { }

        public void Error(string message) { }

        public void Error(Exception ex) { }
    }
}
