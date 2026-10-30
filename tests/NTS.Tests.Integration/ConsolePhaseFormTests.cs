using System.Net;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Domain.Exceptions;
using Not.Exceptions;
using Not.Krud.Abstractions;
using NoTiming.Api.Features.Live;
using NoTiming.Ui.Features.Account;
using NoTiming.Ui.Features.Console;
using NoTiming.Ui.Features.Core;
using NoTiming.Ui.Features.Core.Participations;
using NoTiming.Ui.Storage.Core.Repositories;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Events;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Localization.NtsStrings;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The Save of the Phase form of the Console (#620, ADR-0005, ADR-0013): one event of its own, a <c>PhaseUpdated</c>, that lists
/// only the times that differ from the ones the form showed, a cleared time as an explicit empty value, written in one change
/// against the version the Participation was read at; Start edited in place, a Save that changes nothing writes nothing, one
/// that puts the times out of order records nothing, and the Main Operator undoes an edit by disabling its event in the
/// timeline. Through the service the form uses, over the real Api in this process, with the store and the services of the Ui
/// built as the app builds them.
/// </summary>
public sealed class ConsolePhaseFormTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset START = new(DateTime.Today.AddHours(6)); // a time of the day that the clock of the test does not decide
    static readonly DateTimeOffset ARRIVE = START.AddMinutes(30);
    static readonly DateTimeOffset PRESENT = START.AddMinutes(40);

    readonly MongoFixture _mongo;

    public ConsolePhaseFormTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task One_changed_time_is_written_as_one_event_that_lists_only_that_time_with_the_signed_in_user_as_its_actor()
    {
        var changes = new RecordedChanges();
        await using var api = ApiOf(changes);
        using var scene = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, scene.MainOperator, scene.EventId);
        var before = VersionOf(await StoredAsync(scene.ParticipationId));
        var typed = FormOf(tab);
        typed.ArriveTimeInput = "06:32:00";
        tab.Requests.Asked.Clear();

        await tab.Form.Update(typed);

        Assert.Single(tab.Requests.Asked, x => x.StartsWith("PATCH", StringComparison.Ordinal)); // one write
        var stored = await StoredAsync(scene.ParticipationId);
        Assert.Equal(before + 1, VersionOf(stored));
        var events = EventsOf(stored);
        Assert.Equal(["Arrived", "Presented", "PhaseUpdated"], events.Select(x => x["Kind"].AsString));
        var edit = events[2];
        Assert.Equal("Accepted", edit["Outcome"].AsString);
        Assert.Equal("Manual", edit["Method"].AsString);
        Assert.Equal(scene.MainOperator.Id, edit["ActorId"].AsGuid);
        Assert.Equal(BsonType.DateTime, edit["RecordedAt"].BsonType);
        Assert.False(edit.Contains("Time")); // it has no time of its own: it lists the times it changed
        Assert.All(events.Take(2), x => Assert.False(x.Contains("Changes"))); // and an event of a Snapshot lists none
        var listed = Assert.Single(edit["Changes"].AsBsonArray).AsBsonDocument;
        Assert.Equal("Arrive", listed["Slot"].AsString);
        Assert.Equal(ARRIVE.AddMinutes(2).UtcDateTime, listed["Time"].ToUniversalTime());
        var phase = tab.Dashboard.Selected!.Phases[0];
        Assert.Equal(ARRIVE.AddMinutes(2), phase.ArriveTime!.ToDateTimeOffset());
        Assert.Equal(PRESENT, phase.PresentTime!.ToDateTimeOffset()); // what it did not list is as it was
        Assert.Equal([(scene.EventId, scene.ParticipationId)], changes.Announced); // the viewers are told once
    }

    [Fact]
    public async Task Several_changed_times_are_written_as_one_event_that_lists_them_all_in_one_write()
    {
        await using var api = ApiOf(new RecordedChanges());
        using var scene = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, scene.MainOperator, scene.EventId);
        var typed = FormOf(tab);
        typed.ArriveTimeInput = "06:32:00";
        typed.PresentTimeInput = "06:44:00";
        tab.Requests.Asked.Clear();

        await tab.Form.Update(typed);

        Assert.Single(tab.Requests.Asked, x => x.StartsWith("PATCH", StringComparison.Ordinal));
        var events = EventsOf(await StoredAsync(scene.ParticipationId));
        Assert.Equal(3, events.Count);
        var edit = events[2];
        Assert.Equal("PhaseUpdated", edit["Kind"].AsString);
        var listed = edit["Changes"].AsBsonArray.Select(x => x.AsBsonDocument).ToList();
        Assert.Equal(["Arrive", "Present"], listed.Select(x => x["Slot"].AsString));
        Assert.Equal(
            [ARRIVE.AddMinutes(2).UtcDateTime, PRESENT.AddMinutes(4).UtcDateTime],
            listed.Select(x => x["Time"].ToUniversalTime())
        );
        var phase = tab.Dashboard.Selected!.Phases[0];
        Assert.Equal(ARRIVE.AddMinutes(2), phase.ArriveTime!.ToDateTimeOffset());
        Assert.Equal(PRESENT.AddMinutes(4), phase.PresentTime!.ToDateTimeOffset());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_Save_that_changes_nothing_writes_nothing_though_a_time_has_a_fraction_of_a_second(
        bool fractionOfASecond
    )
    {
        var changes = new RecordedChanges();
        await using var api = ApiOf(changes);
        using var scene = await SceneAsync(api, fractionOfASecond);
        await using var tab = await Tab.OpenAsync(api, scene.MainOperator, scene.EventId);
        var before = VersionOf(await StoredAsync(scene.ParticipationId));
        var typed = FormOf(tab); // the times as the form showed them, to the second
        tab.Requests.Asked.Clear();
        changes.Announced.Clear();

        await tab.Form.Update(typed);

        Assert.DoesNotContain(tab.Requests.Asked, x => x.StartsWith("PATCH", StringComparison.Ordinal));
        var stored = await StoredAsync(scene.ParticipationId);
        Assert.Equal(before, VersionOf(stored));
        Assert.Equal(2, EventsOf(stored).Count);
        Assert.Empty(changes.Announced);
    }

    [Fact]
    public async Task The_views_over_the_store_are_told_of_a_Save_that_was_taken_and_not_of_one_that_changed_nothing()
    {
        await using var api = ApiOf(new RecordedChanges());
        using var scene = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, scene.MainOperator, scene.EventId);
        var told = 0;
        tab.Store.ObservableEvent.Subscribe(() => told++);

        await tab.Form.Update(FormOf(tab));
        var whenNothingChanged = told;
        var typed = FormOf(tab);
        typed.ArriveTimeInput = "06:32:00";
        await tab.Form.Update(typed);

        Assert.Equal(0, whenNothingChanged);
        Assert.True(told > 0); // the lists, the rankings and the handouts are views over the store, and it read the Participation again
    }

    [Fact]
    public async Task A_cleared_time_is_stored_as_an_explicit_empty_value_and_disabling_the_event_brings_the_earlier_time_back()
    {
        await using var api = ApiOf(new RecordedChanges());
        using var scene = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, scene.MainOperator, scene.EventId);
        var typed = FormOf(tab);
        typed.PresentTimeInput = null;

        await tab.Form.Update(typed);

        var stored = await StoredAsync(scene.ParticipationId);
        var edit = EventsOf(stored)[2];
        var listed = Assert.Single(edit["Changes"].AsBsonArray).AsBsonDocument;
        Assert.Equal("Present", listed["Slot"].AsString); // it is listed
        Assert.True(!listed.Contains("Time") || listed["Time"].IsBsonNull); // and says there is none
        var phase = tab.Dashboard.Selected!.Phases[0];
        Assert.Null(phase.PresentTime);
        Assert.Equal(ARRIVE, phase.ArriveTime!.ToDateTimeOffset());
        Assert.Equal("Accepted", EventsOf(stored)[1]["Outcome"].AsString); // the presentation it hides is as it was

        await tab.Dashboard.ChangeTimeEvents([edit["_id"].AsGuid], []);

        Assert.Equal(PRESENT, tab.Dashboard.Selected!.Phases[0].PresentTime!.ToDateTimeOffset());
        var disabled = EventsOf(await StoredAsync(scene.ParticipationId))[2];
        Assert.Equal("RejectedManually", disabled["Outcome"].AsString);
        Assert.True(disabled.Contains("DisabledAt"));

        await tab.Dashboard.ChangeTimeEvents([], [edit["_id"].AsGuid]);

        Assert.Null(tab.Dashboard.Selected!.Phases[0].PresentTime); // enabling it clears the time again
    }

    [Fact]
    public async Task A_time_out_of_order_refuses_the_Save_with_the_message_and_records_nothing()
    {
        var changes = new RecordedChanges();
        await using var api = ApiOf(changes);
        using var scene = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, scene.MainOperator, scene.EventId);
        var before = VersionOf(await StoredAsync(scene.ParticipationId));
        var typed = FormOf(tab);
        typed.StartTimeInput = "05:45:00";
        typed.ArriveTimeInput = "05:00:00"; // before the Start
        tab.Requests.Asked.Clear();
        changes.Announced.Clear();

        var refused = await Assert.ThrowsAnyAsync<ValidationException>(() => tab.Form.Update(typed));

        Assert.Equal(string.Format(__cannot_be_sooner_than__string, Arrival_string, "05:45:00"), refused.Message);
        Assert.Equal(nameof(PhaseUpdateModel.ArriveTimeInput), refused.Property); // where the form shows it
        Assert.DoesNotContain(tab.Requests.Asked, x => x.StartsWith("PATCH", StringComparison.Ordinal));
        var stored = await StoredAsync(scene.ParticipationId);
        Assert.Equal(before, VersionOf(stored));
        Assert.Equal(2, EventsOf(stored).Count);
        Assert.Equal("05:00:00", typed.ArriveTimeInput); // the form keeps what was typed
        var phase = tab.Dashboard.Selected!.Phases[0];
        Assert.Equal(START, phase.StartTime!.ToDateTimeOffset()); // not even the Start was taken
        Assert.Equal(ARRIVE, phase.ArriveTime!.ToDateTimeOffset());
        Assert.Empty(changes.Announced);
    }

    [Fact]
    public async Task The_Start_is_edited_in_place_and_no_event_records_it()
    {
        await using var api = ApiOf(new RecordedChanges());
        using var scene = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, scene.MainOperator, scene.EventId);
        var before = VersionOf(await StoredAsync(scene.ParticipationId));
        var typed = FormOf(tab);
        typed.StartTimeInput = "05:50:00";

        await tab.Form.Update(typed);

        var stored = await StoredAsync(scene.ParticipationId);
        Assert.Equal(before + 1, VersionOf(stored));
        Assert.Equal(2, EventsOf(stored).Count);
        Assert.Equal(START.AddMinutes(-10), tab.Dashboard.Selected!.Phases[0].StartTime!.ToDateTimeOffset());
    }

    [Fact]
    public async Task A_Save_that_moves_the_Out_time_moves_the_Start_of_the_next_Phase_and_the_viewers_are_told_once()
    {
        var changes = new RecordedChanges();
        await using var api = ApiOf(changes);
        using var scene = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, scene.MainOperator, scene.EventId);
        var next = tab.Dashboard.Selected!.Phases[1].StartTime!.ToDateTimeOffset();
        Assert.Equal(PRESENT.AddMinutes(40), next); // the Out time of the first Phase is where the next one starts
        var typed = FormOf(tab);
        typed.PresentTimeInput = "06:50:00";
        changes.Announced.Clear();

        await tab.Form.Update(typed);

        Assert.Equal(next.AddMinutes(10), tab.Dashboard.Selected!.Phases[1].StartTime!.ToDateTimeOffset());
        var stored = await StoredAsync(scene.ParticipationId);
        Assert.Equal(next.AddMinutes(10).UtcDateTime, stored["Phases"][1]["StartTime"].ToUniversalTime());
        Assert.Equal([(scene.EventId, scene.ParticipationId)], changes.Announced);
        Assert.Equal(
            next.AddMinutes(10),
            tab.Dashboard.Participations.Single().Phases[1].StartTime!.ToDateTimeOffset()
        ); // the lists of the dashboard show it
    }

    [Fact]
    public async Task A_Save_that_puts_the_recovery_over_the_limit_eliminates_for_time_and_disabling_the_edit_restores()
    {
        await using var api = ApiOf(new RecordedChanges());
        using var scene = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, scene.MainOperator, scene.EventId);
        var typed = FormOf(tab);
        typed.PresentTimeInput = "07:20:00"; // 50 minutes after the arrival, and the limit is 40

        await tab.Form.Update(typed);

        Assert.True(tab.Dashboard.IsEliminated);
        Assert.IsType<FailedToQualify>(tab.Dashboard.Selected!.Eliminated);
        var stored = await StoredAsync(scene.ParticipationId);
        Assert.True(stored.TryGetValue("Eliminated", out var eliminated) && !eliminated.IsBsonNull);

        await tab.Dashboard.ChangeTimeEvents([EventsOf(stored)[2]["_id"].AsGuid], []);

        Assert.False(tab.Dashboard.IsEliminated);
        stored = await StoredAsync(scene.ParticipationId);
        Assert.True(!stored.TryGetValue("Eliminated", out eliminated) || eliminated.IsBsonNull);
    }

    [Fact]
    public async Task The_timeline_shows_an_edit_of_the_form_as_one_entry_that_names_each_time_it_lists_and_which_it_cleared()
    {
        await using var api = ApiOf(new RecordedChanges());
        using var scene = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, scene.MainOperator, scene.EventId);
        var typed = FormOf(tab);
        typed.ArriveTimeInput = "06:32:00";
        typed.PresentTimeInput = null;
        await tab.Form.Update(typed);

        var entries = (await tab.Timeline.Read())[0].Entries;

        Assert.Equal(3, entries.Count);
        var edit = entries[2];
        Assert.Equal(Timeline_phase_edit_string, edit.Kind);
        Assert.Equal(
            [$"{Arrive_string}: 06:32:00", $"{Presentation_string}: {Timeline_cleared_string}"],
            edit.Times.Select(x => x.ToString())
        );
        Assert.Equal("Marin Main", edit.Actor);
        Assert.Equal("Manual", edit.Source);
        Assert.True(edit.IsShown);
        Assert.True(edit.CanDisable);
        Assert.False(edit.CanEnable);
        Assert.False(entries[0].IsShown); // the arrival that the edit replaced
        Assert.False(entries[1].IsShown); // and the presentation that it cleared
        Assert.Equal("06:30:00", entries[0].Times.Single().ToString()); // a Snapshot says its time and no name
    }

    [Fact]
    public async Task A_Save_over_a_Participation_that_another_tab_changed_is_refused_and_the_form_sends_what_it_kept_again()
    {
        await using var api = ApiOf(new RecordedChanges());
        using var scene = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, scene.MainOperator, scene.EventId);
        await using var other = await Tab.OpenAsync(api, scene.MainOperator, scene.EventId);
        var typed = FormOf(tab);
        typed.ArriveTimeInput = "06:32:00";
        await other.Dashboard.Withdraw(); // the Participation moved on to the next version

        var refused = await Assert.ThrowsAnyAsync<DomainException>(() => tab.Form.Update(typed));

        Assert.Equal(Participation_changed_string, refused.Message);
        Assert.Equal("06:32:00", typed.ArriveTimeInput); // the form kept what was typed
        Assert.Equal(2, EventsOf(await StoredAsync(scene.ParticipationId)).Count); // and nothing was recorded

        await tab.Form.Update(typed); // the same form, sent again over what is there now

        var stored = await StoredAsync(scene.ParticipationId);
        Assert.Equal("PhaseUpdated", EventsOf(stored)[2]["Kind"].AsString);
        Assert.Equal("WD", stored["Eliminated"]["Code"].AsString); // and what the other tab did is still there
    }

    [Fact]
    public async Task The_id_of_an_edit_of_the_form_is_no_Snapshot_so_the_route_neither_records_it_again_nor_corrects_it()
    {
        var changes = new RecordedChanges();
        await using var api = ApiOf(changes);
        using var scene = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, scene.MainOperator, scene.EventId);
        var typed = FormOf(tab);
        typed.ArriveTimeInput = "06:32:00";
        await tab.Form.Update(typed);
        var edit = EventsOf(await StoredAsync(scene.ParticipationId))[2]["_id"].AsGuid;
        var before = VersionOf(await StoredAsync(scene.ParticipationId));
        changes.Announced.Clear();
        var time = ARRIVE.AddMinutes(9);

        var sent = await scene.MainOperator.Page.WriteAsync(
            HttpMethod.Post,
            "/api/snapshots",
            "snapshots",
            new
            {
                eventId = scene.EventId,
                number = 1,
                kind = "Arrive",
                time,
            },
            id: edit.ToString()
        );
        var corrected = await scene.MainOperator.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/snapshots/{edit}",
            "snapshots",
            new { time },
            id: edit.ToString()
        );

        Assert.Equal(HttpStatusCode.Conflict, sent.StatusCode);
        Assert.Equal("id-taken", await ErrorCodeAsync(sent));
        Assert.Equal(HttpStatusCode.NotFound, corrected.StatusCode);
        Assert.Equal("not-found", await ErrorCodeAsync(corrected));
        var stored = await StoredAsync(scene.ParticipationId);
        Assert.Equal(before, VersionOf(stored));
        Assert.Equal(3, EventsOf(stored).Count);
        Assert.Empty(changes.Announced);
    }

    ApiFactory ApiOf(RecordedChanges changes)
    {
        return new ApiFactory(
            _mongo.ConnectionString,
            configureServices: services => services.AddSingleton<IParticipationChanges>(changes)
        );
    }

    /// <summary>The form of the first Phase of the selected Participation, as it is opened: it shows each time to the second.</summary>
    static PhaseUpdateModel FormOf(Tab tab)
    {
        return new PhaseUpdateModel(tab.Dashboard.Selected!.Phases[0]);
    }

    static List<BsonDocument> EventsOf(BsonDocument stored)
    {
        return [.. stored["Phases"][0]["Events"].AsBsonArray.Select(x => x.AsBsonDocument)];
    }

    /// <summary>The version a document is at; a document that no write has counted yet is at 0, which it does not keep.</summary>
    static int VersionOf(BsonDocument document)
    {
        return document.TryGetValue("Version", out var version) ? version.AsInt32 : 0;
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    /// <summary>
    /// A Live Event with one Participation of two Phases whose first one was arrived at (06:30) and presented (06:40) by an
    /// Official, so that the second one starts when the first is out, 40 minutes after the presentation; the arrival has a
    /// fraction of a second when asked for, which the form does not show.
    /// </summary>
    async Task<Scene> SceneAsync(ApiFactory api, bool fractionOfASecond = false)
    {
        var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, name: "Marin Main");
        var official = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, name: "Olga Official");
        var eventId = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            mainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var recorded = new DateTimeOffset(DateTime.Today.AddHours(12));
        var participation = IntegrationPayloadFactory.TwoPhaseParticipation(
            eventId,
            1,
            Guid.NewGuid(),
            startTime: START
        );
        participation.Process(
            IntegrationPayloadFactory.ArriveSnapshot(1, fractionOfASecond ? ARRIVE.AddMilliseconds(437) : ARRIVE),
            official.Id,
            recorded
        );
        participation.Process(
            IntegrationPayloadFactory.PresentSnapshot(1, PRESENT),
            official.Id,
            recorded.AddMinutes(1)
        );
        await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation);
        return new Scene(mainOperator, eventId, participation.Id, client);
    }

    async Task<BsonDocument> StoredAsync(Guid id)
    {
        return await RegistrySeed
            .Collection(_mongo.ConnectionString, "event_participations")
            .Find(new BsonDocument("_id", EventSeed.Binary(id)))
            .SingleAsync();
    }

    sealed class Scene : IDisposable
    {
        readonly HttpClient _client;

        public Scene(Person mainOperator, Guid eventId, Guid participationId, HttpClient client)
        {
            MainOperator = mainOperator;
            EventId = eventId;
            ParticipationId = participationId;
            _client = client;
        }

        public Person MainOperator { get; }
        public Guid EventId { get; }
        public Guid ParticipationId { get; }

        public void Dispose()
        {
            _client.Dispose();
        }
    }

    sealed class Tab : IAsyncDisposable
    {
        public static async Task<Tab> OpenAsync(ApiFactory api, Person person, Guid eventId)
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
            await dashboard.Show(new StubViewedEvent(store, selected.Event!, true));
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

        /// <summary>The service the form of a Phase writes through, which is the dashboard.</summary>
        public IKrudFormService<PhaseUpdateModel> Form => Dashboard;

        public ValueTask DisposeAsync()
        {
            Dashboard.Dispose();
            Store.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
