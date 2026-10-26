using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Domain.Exceptions;
using NoTiming.Api.Features.Live;
using NoTiming.Ui.Features.Account;
using NoTiming.Ui.Features.Console;
using NoTiming.Ui.Features.Core;
using NoTiming.Ui.Features.Core.Participations;
using NoTiming.Ui.Storage.Core.Repositories;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Console;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Objects;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Localization.NtsStrings;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The timeline of a Participation in the Console (#619, ADR-0005, ADR-0013): every time event of every Phase, in the order they
/// were recorded, with who recorded it and when, and what the Console Operator may do with each; and the one change that disables
/// and enables the chosen events, written to the Api against the version the Participation was read at. Over the real Api in this
/// process, with the store and the services of the Ui built as the app builds them.
/// </summary>
public sealed class ConsoleTimelineTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset START = new(DateTime.Today.AddHours(6));
    static readonly DateTimeOffset ARRIVE = START.AddMinutes(30);
    static readonly DateTimeOffset PRESENT = START.AddMinutes(40);

    readonly MongoFixture _mongo;

    public ConsoleTimelineTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_timeline_lists_every_time_event_of_every_Phase_in_the_order_they_were_recorded_with_who_recorded_each_and_when()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, scene.ConsoleOperator, scene.EventId);

        var phases = await tab.Timeline.Read();

        Assert.Equal(2, phases.Count);
        Assert.Equal(["GATE1/20", "GATE2/40"], phases.Select(x => x.Gate));
        var first = phases[0].Entries;
        Assert.Equal([scene.Arrived, scene.Duplicate, scene.Updated, scene.Presented], first.Select(x => x.Id)); // in the order they were recorded
        Assert.Equal(["Olga Official", "Olga Official", "Olga Official", "Marin Main"], first.Select(x => x.Actor));
        Assert.Equal(
            [ARRIVE, ARRIVE.AddMinutes(1), ARRIVE.AddMinutes(3), PRESENT],
            first.Select(x => x.Times.Single().Time!.ToDateTimeOffset())
        );
        Assert.Equal(
            [
                scene.RecordedAt,
                scene.RecordedAt.AddMinutes(1),
                scene.RecordedAt.AddMinutes(2),
                scene.RecordedAt.AddMinutes(3),
            ],
            first.Select(x => x.RecordedAt!.Value)
        );
        Assert.All(first, x => Assert.Equal("Manual", x.Source));
        Assert.Empty(phases[1].Entries);
    }

    [Fact]
    public async Task An_entry_says_what_became_of_its_time_and_whether_it_may_be_disabled_or_enabled_and_whether_it_is_the_time_the_Phase_shows()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, scene.ConsoleOperator, scene.EventId);
        await tab.Dashboard.ChangeTimeEvents([scene.Presented], []);

        var entries = (await tab.Timeline.Read())[0].Entries.ToDictionary(x => x.Id);

        Assert.Equal(TimeEventOutcome.Accepted, entries[scene.Arrived].Outcome);
        Assert.True(entries[scene.Arrived].CanDisable);
        Assert.False(entries[scene.Arrived].CanEnable);
        Assert.False(entries[scene.Arrived].IsShown); // the update that followed is the time shown
        Assert.True(entries[scene.Updated].IsShown);
        Assert.Equal(TimeEventOutcome.RejectedDuplicateArrive, entries[scene.Duplicate].Outcome);
        Assert.False(entries[scene.Duplicate].CanDisable); // the rules rejected it
        Assert.False(entries[scene.Duplicate].CanEnable);
        Assert.Contains("Duplicate Arrive", entries[scene.Duplicate].OutcomeText);
        Assert.Equal(TimeEventOutcome.RejectedManually, entries[scene.Presented].Outcome);
        Assert.True(entries[scene.Presented].CanEnable);
        Assert.False(entries[scene.Presented].CanDisable);
        Assert.False(entries[scene.Presented].IsShown);
        Assert.NotNull(entries[scene.Presented].DisabledAt);
    }

    [Fact]
    public async Task A_person_whose_account_the_Api_does_not_know_is_left_unnamed()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api, strangerActor: true);
        await using var tab = await Tab.OpenAsync(api, scene.ConsoleOperator, scene.EventId);

        var entries = (await tab.Timeline.Read())[0].Entries;

        Assert.Null(entries.Single(x => x.Id == scene.Arrived).Actor);
        Assert.Equal("Olga Official", entries.Single(x => x.Id == scene.Updated).Actor);
    }

    [Fact]
    public async Task The_chosen_events_are_saved_in_one_write_against_the_version_and_the_Api_keeps_the_outcome_and_when()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, scene.ConsoleOperator, scene.EventId);
        var before = VersionOf(await StoredAsync(scene.ParticipationId));
        tab.Requests.Asked.Clear();

        await tab.Dashboard.ChangeTimeEvents([scene.Updated, scene.Presented], []);

        Assert.Single(tab.Requests.Asked, x => x.StartsWith("PATCH", StringComparison.Ordinal)); // one write
        var stored = await StoredAsync(scene.ParticipationId);
        Assert.Equal(before + 1, VersionOf(stored));
        Assert.False(stored["Phases"][0].AsBsonDocument.Contains("ArriveTime")); // the times a Phase tells are not stored
        var events = stored["Phases"][0]["Events"].AsBsonArray.ToDictionary(x => x["_id"].AsGuid);
        Assert.Equal("RejectedManually", events[scene.Updated]["Outcome"].AsString);
        Assert.Equal("RejectedManually", events[scene.Presented]["Outcome"].AsString);
        Assert.Equal("Accepted", events[scene.Arrived]["Outcome"].AsString);
        Assert.True(events[scene.Updated].AsBsonDocument.Contains("DisabledAt"));
        Assert.False(events[scene.Arrived].AsBsonDocument.Contains("DisabledAt"));
        var phase = tab.Dashboard.Selected!.Phases[0];
        Assert.Equal(ARRIVE, phase.ArriveTime!.ToDateTimeOffset()); // the time the update replaced is shown again
        Assert.Null(phase.PresentTime);
    }

    [Fact]
    public async Task A_save_is_announced_to_the_viewers_of_the_Event_once_and_a_refused_one_is_not()
    {
        var changes = new RecordedChanges();
        await using var api = new ApiFactory(
            _mongo.ConnectionString,
            configureServices: services => services.AddSingleton<IParticipationChanges>(changes)
        );
        var scene = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, scene.ConsoleOperator, scene.EventId);
        changes.Announced.Clear();

        await tab.Dashboard.ChangeTimeEvents([scene.Updated], []);
        var announced = changes.Announced.ToList();
        await Assert.ThrowsAsync<DomainException>(() => tab.Dashboard.ChangeTimeEvents([scene.Duplicate], []));

        Assert.Equal([(scene.EventId, scene.ParticipationId)], announced);
        Assert.Equal(announced, changes.Announced);
    }

    [Fact]
    public async Task What_was_disabled_is_enabled_again_by_a_save()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, scene.ConsoleOperator, scene.EventId);
        await tab.Dashboard.ChangeTimeEvents([scene.Updated], []);

        await tab.Dashboard.ChangeTimeEvents([], [scene.Updated]);

        var phase = tab.Dashboard.Selected!.Phases[0];
        Assert.Equal(ARRIVE.AddMinutes(3), phase.ArriveTime!.ToDateTimeOffset());
        var stored = (await StoredAsync(scene.ParticipationId))["Phases"][0]["Events"].AsBsonArray;
        var event_ = stored.Single(x => x["_id"].AsGuid == scene.Updated).AsBsonDocument;
        Assert.Equal("Accepted", event_["Outcome"].AsString);
        Assert.False(event_.Contains("DisabledAt"));
    }

    [Fact]
    public async Task A_save_made_on_a_view_that_another_tab_changed_is_refused_the_Participation_is_read_again_and_nothing_is_applied()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, scene.ConsoleOperator, scene.EventId);
        await using var other = await Tab.OpenAsync(api, scene.ConsoleOperator, scene.EventId);
        other.Dashboard.Selected = other.Dashboard.Participations.Single();
        await other.Dashboard.Withdraw(); // the Participation moved on to the next version

        var refused = await Assert.ThrowsAsync<DomainException>(
            () => tab.Dashboard.ChangeTimeEvents([scene.Updated], [])
        );

        Assert.Equal(Participation_changed_string, refused.Message);
        var stored = (await StoredAsync(scene.ParticipationId))["Phases"][0]["Events"].AsBsonArray;
        Assert.All(stored, x => Assert.DoesNotContain("RejectedManually", x["Outcome"].AsString));
        Assert.True(tab.Dashboard.IsEliminated); // what the tab shows is what is stored again
    }

    [Fact]
    public async Task A_save_that_would_leave_an_earlier_Phase_incomplete_while_a_later_one_has_times_is_refused_naming_the_events_and_nothing_is_stored()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api, laterPhaseHasATime: true);
        await using var tab = await Tab.OpenAsync(api, scene.ConsoleOperator, scene.EventId);
        var before = VersionOf(await StoredAsync(scene.ParticipationId));

        var refused = await Assert.ThrowsAsync<DomainException>(
            () => tab.Dashboard.ChangeTimeEvents([scene.Presented], [])
        );

        Assert.StartsWith($"{Presentation_string} {PRESENT.ToLocalTime():HH:mm:ss} ", refused.Message); // it names the event
        var stored = await StoredAsync(scene.ParticipationId);
        Assert.Equal(before, VersionOf(stored));
        Assert.All(
            stored["Phases"][0]["Events"].AsBsonArray,
            x => Assert.DoesNotContain("RejectedManually", x["Outcome"].AsString)
        );
    }

    [Fact]
    public async Task An_event_the_rules_rejected_cannot_be_disabled_and_nothing_is_stored()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, scene.ConsoleOperator, scene.EventId);
        var before = VersionOf(await StoredAsync(scene.ParticipationId));
        tab.Requests.Asked.Clear();

        await Assert.ThrowsAsync<DomainException>(() => tab.Dashboard.ChangeTimeEvents([scene.Duplicate], []));

        Assert.DoesNotContain(tab.Requests.Asked, x => x.StartsWith("PATCH", StringComparison.Ordinal));
        Assert.Equal(before, VersionOf(await StoredAsync(scene.ParticipationId)));
    }

    [Fact]
    public async Task A_view_that_cannot_write_cannot_save_and_the_Api_is_not_asked()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, scene.ConsoleOperator, scene.EventId, canWrite: false);
        tab.Requests.Asked.Clear();

        await Assert.ThrowsAsync<DomainException>(() => tab.Dashboard.ChangeTimeEvents([scene.Updated], []));

        Assert.DoesNotContain(tab.Requests.Asked, x => x.StartsWith("PATCH", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_container_of_the_app_makes_the_timeline_service_over_the_dashboard()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api);
        await using var ui = ConsoleUi.Open(api, scene.ConsoleOperator);

        var timeline = ui.Get<IConsoleTimeline>();

        Assert.NotNull(timeline);
        Assert.Same(timeline, ui.Get<IConsoleTimeline>()); // one for the page and its dialog
    }

    async Task<Scene> SceneAsync(ApiFactory api, bool strangerActor = false, bool laterPhaseHasATime = false)
    {
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, name: "Marin Main");
        var official = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, name: "Olga Official");
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            consoleOperator.Id,
            DateTimeOffset.UtcNow
        );
        var recorded = new DateTimeOffset(DateTime.Today.AddHours(12));
        var participation = IntegrationPayloadFactory.TwoPhaseParticipation(
            eventId,
            1,
            Guid.NewGuid(),
            startTime: START
        );
        var arriveActor = strangerActor ? Guid.NewGuid() : official.Id;
        var arrived = participation.Process(IntegrationPayloadFactory.ArriveSnapshot(1, ARRIVE), arriveActor, recorded);
        var duplicate = participation.Process(
            IntegrationPayloadFactory.ArriveSnapshot(1, ARRIVE.AddMinutes(1)),
            official.Id,
            recorded.AddMinutes(1)
        );
        var updated = participation.UpdateSnapshot(
            arrived.Id,
            new Timestamp(ARRIVE.AddMinutes(3)),
            official.Id,
            recorded.AddMinutes(2)
        );
        var presented = participation.Process(
            IntegrationPayloadFactory.PresentSnapshot(1, PRESENT),
            consoleOperator.Id,
            recorded.AddMinutes(3)
        );
        if (laterPhaseHasATime)
        {
            var next = participation.Phases[1].StartTime!.ToDateTimeOffset();
            participation.Process(
                IntegrationPayloadFactory.ArriveSnapshot(1, next.AddMinutes(20)),
                official.Id,
                recorded.AddMinutes(4)
            );
        }

        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation);
        return new Scene(
            consoleOperator,
            eventId,
            participation.Id,
            arrived.Id,
            duplicate.Id,
            updated.Event.Id,
            presented.Id,
            recorded
        );
    }

    /// <summary>The version a document is at; a document that no write has counted yet is at 0, which it does not keep.</summary>
    static int VersionOf(BsonDocument document)
    {
        return document.TryGetValue("Version", out var version) ? version.AsInt32 : 0;
    }

    async Task<BsonDocument> StoredAsync(Guid id)
    {
        return await RegistrySeed
            .Collection(_mongo.ConnectionString, "event_participations")
            .Find(new BsonDocument("_id", EventSeed.Binary(id)))
            .SingleAsync();
    }

    sealed class Scene
    {
        public Scene(
            Person consoleOperator,
            Guid eventId,
            Guid participationId,
            Guid arrived,
            Guid duplicate,
            Guid updated,
            Guid presented,
            DateTimeOffset recordedAt
        )
        {
            ConsoleOperator = consoleOperator;
            EventId = eventId;
            ParticipationId = participationId;
            Arrived = arrived;
            Duplicate = duplicate;
            Updated = updated;
            Presented = presented;
            RecordedAt = recordedAt;
        }

        public Person ConsoleOperator { get; }
        public Guid EventId { get; }
        public Guid ParticipationId { get; }
        public Guid Arrived { get; }
        public Guid Duplicate { get; }
        public Guid Updated { get; }
        public Guid Presented { get; }
        public DateTimeOffset RecordedAt { get; }
    }

    sealed class Tab : IAsyncDisposable
    {
        public static async Task<Tab> OpenAsync(ApiFactory api, Person person, Guid eventId, bool canWrite = true)
        {
            var json = JsonApiClients.Of(api, person, out var requests);
            var selected = new SelectedEvent(eventId);
            var repository = new ParticipationEventScopedApiRepository(
                json,
                new EventScopeFactory<Participation>(selected)
            );
            var store = new ParticipationStore(repository);
            var dashboard = new ConsoleDashboard(
                store,
                new ConsoleParticipationWrites(json),
                new ServerClock(json, TimeProvider.System),
                new AccountSession(json),
                new SnapshotApiPublisher(json)
            );
            await dashboard.Show(new StubViewedEvent(store, selected.Event!, canWrite));
            dashboard.Selected = dashboard.Participations.Single();
            return new Tab(dashboard, new ConsoleTimeline(dashboard, json), store, requests);
        }

        Tab(
            ConsoleDashboard dashboard,
            ConsoleTimeline timeline,
            ParticipationStore store,
            JsonApiClients.Requests requests
        )
        {
            Dashboard = dashboard;
            Timeline = timeline;
            Store = store;
            Requests = requests;
        }

        public ConsoleDashboard Dashboard { get; }
        public ConsoleTimeline Timeline { get; }
        public ParticipationStore Store { get; }
        public JsonApiClients.Requests Requests { get; }

        public ValueTask DisposeAsync()
        {
            Dashboard.Dispose();
            Store.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
