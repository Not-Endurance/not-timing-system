using MongoDB.Driver;
using Not.Domain.Exceptions;
using Not.Krud.Abstractions;
using NoTiming.Ui.Features.Account;
using NoTiming.Ui.Features.Console;
using NoTiming.Ui.Features.Core;
using NoTiming.Ui.Features.Core.Participations;
using NoTiming.Ui.Storage.Core.Repositories;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Console;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Localization.NtsStrings;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;
using Competition = NTS.Domain.Core.Aggregates.Participations.Objects.Competition;

namespace NTS.Tests.Integration;

/// <summary>
/// The dashboard of a Live Event in the Console (#646, ADR-0013): what the Console Operator does to the Participation it works on,
/// each change written to the Api against the version it saw, and what happens when another tab has changed it meanwhile.
/// Over the real Api in this process, with the store and the services of the Ui built as the app builds them; the hub is left
/// to the tests of the hub, so a tab reads a Participation again only when it asks to.
/// </summary>
public sealed class ConsoleDashboardTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset START = new(DateTime.Today.AddHours(6)); // a time of the day that the clock of the test does not decide

    readonly MongoFixture _mongo;
    string _tenant = string.Empty;

    public ConsoleDashboardTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_Console_Operator_withdraws_a_Participation_and_restores_it_and_the_Api_stores_each_change()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var (person, eventId, ids) = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, person, eventId);
        tab.Dashboard.Selected = tab.Dashboard.Participations.Single(x => x.Combination.Number == 2);

        await tab.Dashboard.Withdraw();
        var withdrawn = await StoredAsync(ids[1]);
        var isEliminated = tab.Dashboard.IsEliminated;
        await tab.Dashboard.RestoreQualification();
        var restored = await StoredAsync(ids[1]);

        Assert.True(isEliminated);
        Assert.True(withdrawn.TryGetValue("Eliminated", out var stored) && !stored.IsBsonNull);
        Assert.False(tab.Dashboard.IsEliminated);
        Assert.True(!restored.TryGetValue("Eliminated", out var gone) || gone.IsBsonNull);
        Assert.All(
            tab.Requests.Asked.Where(x => x.StartsWith("PATCH", StringComparison.Ordinal)),
            asked => Assert.Contains($"/api/participations/{ids[1]}", asked)
        );
    }

    [Fact]
    public async Task A_Representation_and_an_Inspection_are_requested_in_the_Phase_that_a_request_made_now_acts_on_and_withdrawn_again()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var (person, eventId, ids) = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, person, eventId);
        tab.Dashboard.Selected = tab.Dashboard.Participations.First();
        var tooEarly = await Assert.ThrowsAsync<DomainException>(() => tab.Dashboard.RequestRepresentation(true)); // there is no Presentation yet
        await tab.Dashboard.RecordTime(SnapshotType.Arrive, TimeSpan.FromHours(7));
        await tab.Dashboard.RecordTime(SnapshotType.Present, TimeSpan.FromHours(7.25));

        await tab.Dashboard.RequestRepresentation(true);
        var tooSoon = await Assert.ThrowsAsync<DomainException>(() => tab.Dashboard.RequestInspection(true)); // it is of a Representation
        await tab.Dashboard.RecordTime(SnapshotType.Present, TimeSpan.FromHours(7.5)); // the Combination is presented again
        await tab.Dashboard.RequestInspection(true);
        var requested = (tab.Dashboard.IsRepresentRequested, tab.Dashboard.IsInspectionRequested);
        var stored = await StoredAsync(ids[0]);
        await tab.Dashboard.RequestInspection(false);
        var stays = await Assert.ThrowsAsync<DomainException>(() => tab.Dashboard.RequestRepresentation(false));

        Assert.Contains("Presentation", tooEarly.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Representation", tooSoon.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal((true, true), requested);
        var phase = stored["Phases"][0].AsBsonDocument;
        Assert.True(phase["IsRepresentRequested"].AsBoolean);
        Assert.True(phase["IsRequiredInspectionRequested"].AsBoolean);
        Assert.False(tab.Dashboard.IsInspectionRequested); // an Inspection is withdrawn
        Assert.True(tab.Dashboard.IsRepresentRequested); // and a Representation is not, once its time is there
        Assert.Contains("already present", stays.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_time_the_Console_Operator_names_is_recorded_by_the_server_as_an_Arrival_of_the_Participation()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var (person, eventId, _) = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, person, eventId);
        tab.Dashboard.Selected = tab.Dashboard.Participations.First();

        await tab.Dashboard.RecordTime(SnapshotType.Arrive, TimeSpan.FromHours(7.25));

        var arrive = tab.Dashboard.Selected!.Phases.First().ArriveTime!.ToDateTimeOffset().ToLocalTime();
        Assert.Equal(TimeSpan.FromHours(7.25), arrive.TimeOfDay);
        Assert.Equal([1], tab.Dashboard.RecentlyTimed);

        tab.Dashboard.Selected = tab.Dashboard.Participations.First();
        Assert.Empty(tab.Dashboard.RecentlyTimed); // once it is looked at, it is not new any more
    }

    [Fact]
    public async Task The_first_Participation_is_selected_when_the_dashboard_opens_and_the_next_one_when_it_is_gone()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var (person, eventId, ids) = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, person, eventId);
        var opened = tab.Dashboard.Selected!.Combination.Number;
        Assert.Equal([1, 2], tab.Dashboard.Participations.Select(x => x.Combination.Number)); // by their numbers

        await RegistrySeed
            .Collection(_mongo.ConnectionString, "event_participations")
            .DeleteOneAsync(new MongoDB.Bson.BsonDocument("_id", EventSeed.Binary(ids[0])));
        await tab.Store.Refresh(ids[0]);

        Assert.Equal(1, opened);
        Assert.Equal(2, tab.Dashboard.Selected!.Combination.Number);
        Assert.Equal([2], tab.Dashboard.Participations.Select(x => x.Combination.Number));
    }

    [Fact]
    public async Task What_a_request_acts_on_is_told_by_the_clock_of_the_server_which_is_asked_when_the_dashboard_opens()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var (person, eventId, ids) = await SceneAsync(api, [Requested(1)]);
        var late = new FixedClock(new DateTimeOffset(DateTime.Today.AddHours(23).AddMinutes(59).AddSeconds(30)));
        var early = new FixedClock(new DateTimeOffset(DateTime.Today.AddHours(7).AddMinutes(30)));
        await using var afterTheStart = await Tab.OpenAsync(api, person, eventId, clock: late);
        await using var beforeIt = await Tab.OpenAsync(api, person, eventId, clock: early);
        afterTheStart.Dashboard.Selected = afterTheStart.Dashboard.Participations.Single();
        beforeIt.Dashboard.Selected = beforeIt.Dashboard.Participations.Single();

        // the Phase that a request made at that time belongs to is the first one before the start of the second, and the second after it
        var (representBefore, inspectionBefore) = (
            beforeIt.Dashboard.IsRepresentRequested,
            beforeIt.Dashboard.IsInspectionRequested
        );
        var (representAfter, inspectionAfter) = (
            afterTheStart.Dashboard.IsRepresentRequested,
            afterTheStart.Dashboard.IsInspectionRequested
        );
        await afterTheStart.Dashboard.RequestInspection(false); // withdrawn in the second Phase, where there is none to withdraw

        Assert.Equal((true, true), (representBefore, inspectionBefore));
        Assert.Equal((false, false), (representAfter, inspectionAfter));
        Assert.True((await StoredAsync(ids[0]))["Phases"][0]["IsRequiredInspectionRequested"].AsBoolean);
        Assert.Equal(1, late.Synchronised);
        Assert.Equal(1, early.Synchronised);
    }

    [Fact]
    public async Task A_time_that_the_server_records_and_does_not_accept_is_said_by_its_outcome_and_the_Participation_is_marked_as_timed()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var (person, eventId, ids) = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, person, eventId);
        tab.Dashboard.Selected = tab.Dashboard.Participations.First();
        await tab.Dashboard.RecordTime(SnapshotType.Arrive, TimeSpan.FromHours(7));
        tab.Dashboard.Selected = tab.Dashboard.Participations.Last(); // the first is not new any more when it is looked at, so it waits to be timed again
        tab.Dashboard.Selected = tab.Dashboard.Participations.First();

        var again = await Assert.ThrowsAsync<DomainException>(
            () => tab.Dashboard.RecordTime(SnapshotType.Arrive, TimeSpan.FromHours(7.5))
        );

        Assert.Equal(string.Format(Time_not_taken_string, "Rejected Duplicate Arrive"), again.Message);
        Assert.Contains(1, tab.Dashboard.RecentlyTimed); // it was timed, whatever the server made of the time
        var events = (await StoredAsync(ids[0]))["Phases"][0]["Events"].AsBsonArray;
        Assert.Equal(2, events.Count); // the one that was taken and the one that was not, which the Phase keeps with its reason
    }

    [Fact]
    public async Task A_time_the_server_does_not_take_is_said_with_its_reason()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var (person, eventId, _) = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, person, eventId);
        tab.Dashboard.Selected = tab.Dashboard.Participations.First();

        var before = await Assert.ThrowsAsync<DomainException>(
            () => tab.Dashboard.RecordTime(SnapshotType.Arrive, TimeSpan.FromHours(5))
        );

        Assert.Contains("Invalid", before.Message, StringComparison.OrdinalIgnoreCase); // an Arrival before the Start
        Assert.Null(tab.Dashboard.Selected!.Phases.First().ArriveTime);
    }

    [Fact]
    public async Task A_time_that_the_Api_does_not_record_for_the_person_is_said_in_its_words_and_nothing_is_marked_as_timed()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var (_, eventId, ids) = await SceneAsync(api);
        var plain = await SomebodyElseAsync(api); // not the Console Operator of the Event, nor an Official of it
        await using var tab = await Tab.OpenAsync(api, plain, eventId);
        tab.Dashboard.Selected = tab.Dashboard.Participations.First();

        var refused = await Assert.ThrowsAsync<DomainException>(
            () => tab.Dashboard.RecordTime(SnapshotType.Arrive, TimeSpan.FromHours(7))
        );

        Assert.NotEmpty(refused.Message);
        Assert.NotEqual(Time_not_recorded_string, refused.Message); // the Api said why
        Assert.Empty(tab.Dashboard.RecentlyTimed);
        Assert.Empty((await StoredAsync(ids[0]))["Phases"][0]["Events"].AsBsonArray);
    }

    [Fact]
    public async Task A_change_that_is_made_on_a_Participation_that_another_tab_changed_is_refused_and_the_Participation_is_read_again()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var (person, eventId, ids) = await SceneAsync(api);
        await using var first = await Tab.OpenAsync(api, person, eventId);
        await using var second = await Tab.OpenAsync(api, person, eventId); // read before the first one changes anything
        first.Dashboard.Selected = first.Dashboard.Participations.First();
        second.Dashboard.Selected = second.Dashboard.Participations.First();
        await first.Dashboard.Withdraw();

        var refused = await Assert.ThrowsAsync<DomainException>(() => second.Dashboard.Retire());

        Assert.Equal(Participation_changed_string, refused.Message);
        Assert.Equal("WD", second.Dashboard.Selected!.Eliminated!.Code); // what is shown is what is stored, not the change that was refused
        Assert.Equal("WD", (await StoredAsync(ids[0]))["Eliminated"]["Code"].AsString);
        await second.Dashboard.RestoreQualification(); // and the next change is made on what is there
        Assert.False(second.Dashboard.IsEliminated);
    }

    [Fact]
    public async Task The_times_of_a_Phase_that_were_typed_over_a_Participation_that_changed_are_kept_and_taken_when_they_are_sent_again()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var (person, eventId, ids) = await SceneAsync(api);
        await using var first = await Tab.OpenAsync(api, person, eventId);
        await using var second = await Tab.OpenAsync(api, person, eventId);
        first.Dashboard.Selected = first.Dashboard.Participations.First();
        var typed = new PhaseUpdateModel(second.Dashboard.Participations.First().Phases.First())
        {
            ArriveTimeInput = "08:30:00",
        };
        await first.Dashboard.Withdraw();
        var form = (IKrudFormService<PhaseUpdateModel>)second.Dashboard;

        var refused = await Assert.ThrowsAsync<DomainException>(() => form.Update(typed));
        await form.Update(typed); // the same form, sent again, over what is there now

        Assert.Equal(Participation_changed_string, refused.Message);
        Assert.Equal("08:30:00", typed.ArriveTimeInput); // the form kept what was typed
        var stored = (await StoredAsync(ids[0]))["Phases"][0]["Events"].AsBsonArray;
        Assert.Contains(stored, x => x.AsBsonDocument.ToString()!.Contains("Manual", StringComparison.Ordinal));
        Assert.Equal("WD", (await StoredAsync(ids[0]))["Eliminated"]["Code"].AsString); // and what the other tab did is still there
    }

    [Fact]
    public async Task The_times_typed_for_a_Phase_are_taken_for_its_Participation_by_the_person_who_typed_them()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var (person, eventId, ids) = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, person, eventId);
        var typed = new PhaseUpdateModel(tab.Dashboard.Participations.Last().Phases.First())
        {
            ArriveTimeInput = "08:30:00",
        };

        await ((IKrudFormService<PhaseUpdateModel>)tab.Dashboard).Update(typed);

        var second = (await StoredAsync(ids[1]))["Phases"][0]["Events"].AsBsonArray; // the Participation that the Phase belongs to
        var first = (await StoredAsync(ids[0]))["Phases"][0]["Events"].AsBsonArray;
        Assert.Contains(second, x => x.AsBsonDocument.ToString()!.Contains("Manual", StringComparison.Ordinal));
        Assert.Contains(second, x => x["ActorId"].AsGuid == person.Id);
        Assert.Empty(first);
    }

    [Fact]
    public async Task The_times_typed_for_a_Phase_are_not_taken_from_nobody()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var (person, eventId, ids) = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, person, eventId);
        tab.Requests.Answer = request =>
            request.RequestUri!.AbsolutePath == "/api/me"
                ? new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
                : null; // the session has ended
        var typed = new PhaseUpdateModel(tab.Dashboard.Participations.First().Phases.First())
        {
            ArriveTimeInput = "08:30:00",
        };

        var refused = await Assert.ThrowsAsync<DomainException>(
            () => ((IKrudFormService<PhaseUpdateModel>)tab.Dashboard).Update(typed)
        );

        Assert.Equal(Not_signed_in_string, refused.Message);
        Assert.Empty(tab.Requests.Asked.Where(x => x.StartsWith("PATCH", StringComparison.Ordinal)));
        Assert.Empty((await StoredAsync(ids[0]))["Phases"][0]["Events"].AsBsonArray);
    }

    [Fact]
    public async Task A_change_that_the_Api_refuses_for_another_reason_is_said_in_its_words_and_is_not_taken_for_a_stale_one()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var (_, eventId, ids) = await SceneAsync(api);
        var plain = await SomebodyElseAsync(api); // not the Console Operator of the Event
        await using var tab = await Tab.OpenAsync(api, plain, eventId);
        tab.Dashboard.Selected = tab.Dashboard.Participations.First();

        var refused = await Assert.ThrowsAsync<DomainException>(() => tab.Dashboard.Withdraw());

        Assert.NotEmpty(refused.Message);
        Assert.NotEqual(Participation_changed_string, refused.Message);
        Assert.False(tab.Dashboard.IsEliminated); // what is shown is what is stored
        Assert.True(!(await StoredAsync(ids[0])).TryGetValue("Eliminated", out var stored) || stored.IsBsonNull);
    }

    [Fact]
    public async Task A_view_that_cannot_write_refuses_before_the_Api_is_asked()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var (person, eventId, _) = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, person, eventId, canWrite: false);
        tab.Dashboard.Selected = tab.Dashboard.Participations.First();
        var asked = tab.Requests.Asked.Count;

        var typed = new PhaseUpdateModel(tab.Dashboard.Participations.First().Phases.First())
        {
            ArriveTimeInput = "08:30:00",
        };

        await Assert.ThrowsAsync<DomainException>(() => tab.Dashboard.Withdraw());
        await Assert.ThrowsAsync<DomainException>(
            () => tab.Dashboard.RecordTime(SnapshotType.Arrive, TimeSpan.FromHours(7))
        );
        await Assert.ThrowsAsync<DomainException>(
            () => ((IKrudFormService<PhaseUpdateModel>)tab.Dashboard).Update(typed)
        );

        Assert.Equal(asked, tab.Requests.Asked.Count);
    }

    [Fact]
    public async Task A_Participation_that_could_not_be_read_again_stays_where_it_is_and_one_that_is_gone_goes()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var (person, eventId, ids) = await SceneAsync(api);
        await using var tab = await Tab.OpenAsync(api, person, eventId);
        var asked = $"/api/participations/{ids[0]}";

        tab.Requests.Answer = request =>
            request.RequestUri!.AbsolutePath == asked
                ? new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
                : null;
        await tab.Store.Refresh(ids[0]);
        var whenItFailed = tab.Store.Find(ids[0]);
        tab.Requests.Answer = request =>
            request.RequestUri!.AbsolutePath == asked
                ? new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
                : null;
        await tab.Store.Refresh(ids[0]);

        Assert.NotNull(whenItFailed); // a read that did not come is not a Participation that was removed
        Assert.Null(tab.Store.Find(ids[0])); // and one that the Api says is not there is
        Assert.NotNull(tab.Store.Find(ids[1]));
    }

    [Fact]
    public async Task The_container_of_the_app_makes_the_dashboard_and_the_form_of_a_Phase_one_service()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        await using var ui = ConsoleUi.Open(api, person);

        var dashboard = ui.Get<IConsoleDashboard>();
        var form = ui.Get<IKrudFormService<PhaseUpdateModel>>();

        Assert.Same(dashboard, form); // the form of a Phase writes what the dashboard shows
        Assert.NotNull(ui.Get<IServerClock>());
    }

    async Task<(Person Person, Guid EventId, Guid[] Participations)> SceneAsync(
        ApiFactory api,
        Func<Guid, Participation>[]? participations = null
    )
    {
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        _tenant = tenant;
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, person.Id, DateTimeOffset.UtcNow);
        var ids = new List<Guid>();
        foreach (
            var make in participations
                ?? new Func<Guid, Participation>[]
                {
                    id => IntegrationPayloadFactory.ActiveParticipation(id, 1, Guid.NewGuid(), startTime: START),
                    id => IntegrationPayloadFactory.ActiveParticipation(id, 2, Guid.NewGuid(), startTime: START),
                }
        )
        {
            var participation = make(eventId);
            await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation);
            ids.Add(participation.Id);
        }

        return (person, eventId, [.. ids]);
    }

    /// <summary>Another account of the Tenant of the Event of the scene, which does not run it.</summary>
    async Task<Person> SomebodyElseAsync(ApiFactory api)
    {
        using var client = ApiClients.Of(api);
        return await SignedInAsync(api, client, _mongo.ConnectionString, _tenant);
    }

    /// <summary>A Participation of two Phases, of which the first has been presented and has a Representation and an Inspection requested and the second starts a minute before midnight.</summary>
    static Func<Guid, Participation> Requested(int number)
    {
        return eventId =>
        {
            var country = new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
            var athlete = new Athlete($"Athlete {number}", null, country, null, null, Guid.NewGuid());
            var horse = new Horse($"Horse {number}", null, null, Guid.NewGuid());
            var combination = new Combination(number, athlete, horse, null, "80", null, null, Guid.NewGuid());
            var first = new Phase(
                "GATE1/40",
                40,
                40,
                40,
                CompetitionRuleset.FEI,
                false,
                null,
                Timestamp.Create(START),
                Timestamp.Create(START.AddHours(2)),
                Timestamp.Create(START.AddHours(2.25)),
                null,
                true,
                true,
                false
            );
            var last = new Phase(
                "GATE2/40",
                40,
                40,
                null,
                CompetitionRuleset.FEI,
                true,
                null,
                Timestamp.Create(new DateTimeOffset(DateTime.Today.AddHours(23).AddMinutes(59))),
                null,
                null,
                null,
                false,
                false,
                false
            );
            return new Participation(
                ParticipationCategory.Senior,
                new Competition("CEI 1*", CompetitionRuleset.FEI),
                combination,
                new PhaseCollection([first, last]),
                null,
                eventId,
                Guid.NewGuid()
            );
        };
    }

    async Task<MongoDB.Bson.BsonDocument> StoredAsync(Guid id)
    {
        return (await RegistrySeed.StoredAsync(_mongo.ConnectionString, "event_participations", id))!;
    }

    sealed class FixedClock : IServerClock
    {
        public FixedClock(DateTimeOffset now)
        {
            Now = now;
        }

        public DateTimeOffset Now { get; }
        public int Synchronised { get; private set; }

        public Task Synchronise()
        {
            Synchronised++;
            return Task.CompletedTask;
        }
    }

    /// <summary>One tab of the Console Operator: its own store of the Participations and its own dashboard, over the Api.</summary>
    sealed class Tab : IAsyncDisposable
    {
        public static async Task<Tab> OpenAsync(
            ApiFactory api,
            Person person,
            Guid eventId,
            bool canWrite = true,
            IServerClock? clock = null
        )
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
                clock ?? new ServerClock(json, TimeProvider.System),
                new AccountSession(json),
                new SnapshotApiPublisher(json)
            );
            await dashboard.Show(new StubViewedEvent(store, selected.Event!, canWrite));
            return new Tab(dashboard, store, requests);
        }

        Tab(ConsoleDashboard dashboard, ParticipationStore store, JsonApiClients.Requests requests)
        {
            Dashboard = dashboard;
            Store = store;
            Requests = requests;
        }

        public ConsoleDashboard Dashboard { get; }
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
