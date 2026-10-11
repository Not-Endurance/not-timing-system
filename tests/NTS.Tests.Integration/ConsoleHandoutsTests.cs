using MongoDB.Bson;
using MongoDB.Driver;
using Not.Domain.Exceptions;
using Not.Notify;
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
/// The Handouts of a Live Event in the Console (#646, ADR-0006): the sheet of a Participation, made by the Main Operator or by
/// the Console while a page that watches is open and a Phase completes, composed from the Participation as it is, and removed
/// once it was printed. Over the real Api in this process, with the store and the services of the Ui built as the app builds
/// them; the hub is left to the tests of the hub, so the store reads a Participation again when a time was recorded.
/// </summary>
public sealed class ConsoleHandoutsTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset START = new(DateTime.Today.AddHours(6)); // a time of the day that the clock of the test does not decide

    readonly MongoFixture _mongo;

    public ConsoleHandoutsTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_Handout_made_by_the_number_of_a_Participation_is_stored_under_its_id_and_made_again_it_is_the_one_there_is()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api, [Plain(1), Plain(2)]);
        await using var tab = await Tab.OpenAsync(api, scene);

        await tab.Handouts.Create(2);
        await tab.Handouts.Create(2);

        var stored = await StoredAsync(scene.EventId);
        var kept = Assert.Single(stored);
        Assert.Equal(scene.Ids[1], kept["_id"].AsGuid);
        Assert.Equal(scene.Ids[1], kept["ParticipationId"].AsGuid);
        var document = Assert.Single(tab.Handouts.Documents);
        Assert.Equal(scene.Ids[1], document.Id);
        Assert.Equal(2, document.Entries.Single().Participation!.Combination.Number);
    }

    [Fact]
    public async Task A_Participation_that_the_Event_does_not_have_is_said_and_nothing_is_stored()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api, [Plain(1)]);
        await using var tab = await Tab.OpenAsync(api, scene);

        var refused = await Assert.ThrowsAsync<DomainException>(() => tab.Handouts.Create(9));

        Assert.Equal(Participation_not_found_string, refused.Message);
        Assert.Empty(await StoredAsync(scene.EventId));
    }

    [Fact]
    public async Task A_Participation_that_has_a_Handout_under_another_id_is_not_given_a_second()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api, [Plain(1)]);
        await EventSeed.HandoutAsync(
            _mongo.ConnectionString,
            scene.Tenant,
            new Handout(scene.EventId, scene.Ids[0], Guid.NewGuid()) // one that came with the Event
        );
        await using var tab = await Tab.OpenAsync(api, scene);

        await tab.Handouts.Create(1);

        Assert.Single(await StoredAsync(scene.EventId));
        Assert.Single(tab.Handouts.Documents);
    }

    [Fact]
    public async Task The_documents_are_by_start_number_and_show_the_Participation_as_it_is_when_they_are_composed()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api, [Plain(1), Plain(2), Plain(3)]);
        await using var tab = await Tab.OpenAsync(api, scene);
        await tab.Handouts.Create(3);
        await tab.Handouts.Create(1);
        var numbersBefore = tab.Handouts.Documents.Select(x => x.Entries.Single().Participation!.Combination.Number);
        var arrivedBefore = tab.Handouts.Documents[1].Entries.Single().Participation!.Phases.First().ArriveTime != null;
        tab.Dashboard.Selected = tab.Dashboard.Participations.First(x => x.Combination.Number == 3);

        await tab.Dashboard.RecordTime(SnapshotType.Arrive, TimeSpan.FromHours(9)); // the Handout names the Participation and holds no time

        var after = tab.Handouts.Documents;
        Assert.Equal([1, 3], numbersBefore);
        Assert.Equal([1, 3], after.Select(x => x.Entries.Single().Participation!.Combination.Number));
        Assert.False(arrivedBefore);
        Assert.NotNull(after[1].Entries.Single().Participation!.Phases.First().ArriveTime);
        Assert.Equal([1, 2, 3], tab.Handouts.Combinations.Select(x => x.Number));
    }

    [Fact]
    public async Task Handouts_that_were_printed_are_removed_and_the_ones_that_were_not_stay()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api, [Plain(1), Plain(2), Plain(3)]);
        await using var tab = await Tab.OpenAsync(api, scene);
        foreach (var number in new[] { 1, 2, 3 })
        {
            await tab.Handouts.Create(number);
        }

        var printed = tab.Handouts.Documents.Where(x => x.Entries.Single().Participation!.Combination.Number != 2);
        await tab.Handouts.Delete(printed);

        var kept = Assert.Single(await StoredAsync(scene.EventId));
        Assert.Equal(scene.Ids[1], kept["_id"].AsGuid);
        Assert.Equal(scene.Ids[1], Assert.Single(tab.Handouts.Documents).Id);
    }

    [Fact]
    public async Task A_Handout_that_the_Api_does_not_take_is_said_in_its_words_and_nothing_is_stored()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api, [Plain(1)], mainOperator: false);
        await using var tab = await Tab.OpenAsync(api, scene);

        var refused = await Assert.ThrowsAsync<DomainException>(() => tab.Handouts.Create(1));

        Assert.NotEmpty(refused.Message);
        Assert.Empty(await StoredAsync(scene.EventId));
        Assert.Empty(tab.Handouts.Documents);
    }

    [Fact]
    public async Task A_view_that_cannot_write_refuses_before_the_Api_is_asked()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api, [Plain(1)]);
        await using var tab = await Tab.OpenAsync(api, scene, canWrite: false);

        await Assert.ThrowsAsync<DomainException>(() => tab.Handouts.Create(1));
        await Assert.ThrowsAsync<DomainException>(() => tab.Handouts.Delete([]));

        Assert.Empty(tab.Requests.Asked.Where(x => x.StartsWith("POST", StringComparison.Ordinal)));
        Assert.Empty(tab.Requests.Asked.Where(x => x.StartsWith("DELETE", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_Phase_that_completes_while_the_Event_is_watched_makes_the_Handout_of_its_Participation_and_tells_it()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api, [TwoPhases(1), TwoPhases(2)]);
        await using var tab = await Tab.OpenAsync(api, scene);
        using var watching = tab.Handouts.Watch(tab.View);
        tab.Dashboard.Selected = tab.Dashboard.Participations.First(x => x.Combination.Number == 2);

        await tab.Dashboard.RecordTime(SnapshotType.Arrive, TimeSpan.FromHours(9));
        await tab.Dashboard.RecordTime(SnapshotType.Present, TimeSpan.FromHours(9.25));

        await UiBrowser.Eventually(async () => (await StoredAsync(scene.EventId)).Count == 1);
        var kept = Assert.Single(await StoredAsync(scene.EventId));
        Assert.Equal(scene.Ids[1], kept["_id"].AsGuid); // the Handout of the Participation that completed its Phase
        Assert.Equal([string.Format(Handout_made_string, 2)], tab.Informed);
        Assert.Empty(tab.Warned);
    }

    [Fact]
    public async Task The_last_Phase_and_a_Phase_that_was_complete_before_the_watching_began_make_no_Handout()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api, [Plain(1), TwoPhases(2, arrived: true), TwoPhases(3)]);
        await using var tab = await Tab.OpenAsync(api, scene);
        using var watching = tab.Handouts.Watch(tab.View);

        tab.Dashboard.Selected = tab.Dashboard.Participations.First(x => x.Combination.Number == 1);
        await tab.Dashboard.RecordTime(SnapshotType.Arrive, TimeSpan.FromHours(9));
        await tab.Dashboard.RecordTime(SnapshotType.Present, TimeSpan.FromHours(9.25)); // its only Phase is the last one
        tab.Dashboard.Selected = tab.Dashboard.Participations.First(x => x.Combination.Number == 3);
        await tab.Dashboard.RecordTime(SnapshotType.Arrive, TimeSpan.FromHours(9));
        await tab.Dashboard.RecordTime(SnapshotType.Present, TimeSpan.FromHours(9.25));

        await UiBrowser.Eventually(async () => (await StoredAsync(scene.EventId)).Count >= 1);
        var kept = Assert.Single(await StoredAsync(scene.EventId)); // of the third, which the watch saw complete, and of neither of the others
        Assert.Equal(scene.Ids[2], kept["_id"].AsGuid);
    }

    [Fact]
    public async Task Nothing_is_made_before_the_watching_begins_or_once_the_last_page_that_watches_has_left()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api, [TwoPhases(1), TwoPhases(2), TwoPhases(3), TwoPhases(4)]);
        await using var tab = await Tab.OpenAsync(api, scene);
        await CompleteFirstPhaseAsync(tab, 1); // nobody watches
        var leaving = tab.Handouts.Watch(tab.View);
        var staying = tab.Handouts.Watch(tab.View); // two pages of the Console open
        leaving.Dispose();
        leaving.Dispose(); // once is as good as twice: the other page still watches

        await CompleteFirstPhaseAsync(tab, 2);
        await UiBrowser.Eventually(async () => (await StoredAsync(scene.EventId)).Count == 1);
        staying.Dispose();
        await CompleteFirstPhaseAsync(tab, 3); // nobody watches any more
        await SettleAsync();
        using var again = tab.Handouts.Watch(tab.View); // what is complete now was handed out before
        await CompleteFirstPhaseAsync(tab, 4);

        await UiBrowser.Eventually(async () => (await StoredAsync(scene.EventId)).Count == 2);
        await SettleAsync();
        var kept = await StoredAsync(scene.EventId);
        Assert.Equal(
            [scene.Ids[1], scene.Ids[3]],
            kept.Select(x => x["_id"].AsGuid).OrderBy(x => Array.IndexOf(scene.Ids, x))
        );
    }

    [Fact]
    public async Task A_Handout_that_the_Api_does_not_take_while_the_Event_is_watched_is_told_and_the_Event_goes_on()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api, [TwoPhases(1), TwoPhases(2)], mainOperator: false);
        await using var tab = await Tab.OpenAsync(api, scene, scene.Person); // it may not make Handouts, and sees the Event as it is
        await using var other = await Tab.OpenAsync(api, scene, scene.Boss);
        using var watching = tab.Handouts.Watch(tab.View);
        other.Dashboard.Selected = other.Dashboard.Participations.First(x => x.Combination.Number == 1);
        await other.Dashboard.RecordTime(SnapshotType.Arrive, TimeSpan.FromHours(9));
        await other.Dashboard.RecordTime(SnapshotType.Present, TimeSpan.FromHours(9.25));

        await tab.Store.Refresh(scene.Ids[0]);

        await UiBrowser.Eventually(async () => tab.Warned.Count > 0);
        Assert.StartsWith("The Handout of #1 was not made: ", tab.Warned.Single(), StringComparison.Ordinal);
        Assert.Empty(await StoredAsync(scene.EventId));
        Assert.Empty(tab.Informed);
        await tab.Store.Refresh(scene.Ids[1]); // the Event goes on
        Assert.Equal(2, tab.Store.Participations.Count);
    }

    [Fact]
    public async Task The_Handouts_of_the_Console_are_in_the_container_the_app_builds()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api, [Plain(1)]);
        await using var ui = ConsoleUi.Open(api, scene.Person);

        var handouts = ui.Get<IConsoleHandouts>();

        Assert.IsType<ConsoleHandouts>(handouts);
        Assert.Same(handouts, ui.Get<IConsoleHandouts>()); // one per app, as the dashboard is
        Assert.NotNull(ui.Get<ConsoleHandoutWrites>());
    }

    static async Task CompleteFirstPhaseAsync(Tab tab, int number)
    {
        tab.Dashboard.Selected = tab.Dashboard.Participations.First(x => x.Combination.Number == number);
        await tab.Dashboard.RecordTime(SnapshotType.Arrive, TimeSpan.FromHours(9));
        await tab.Dashboard.RecordTime(SnapshotType.Present, TimeSpan.FromHours(9.25));
    }

    /// <summary>What a handler started when a time was recorded has had the time to reach the Api, so that what it made is there to be counted.</summary>
    static Task SettleAsync()
    {
        return Task.Delay(500);
    }

    async Task<Scene> SceneAsync(ApiFactory api, Func<Guid, Participation>[] participations, bool mainOperator = true)
    {
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var boss = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var person = mainOperator ? boss : await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, boss.Id, DateTimeOffset.UtcNow);
        var ids = new List<Guid>();
        foreach (var make in participations)
        {
            var participation = make(eventId);
            await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation);
            ids.Add(participation.Id);
        }

        return new Scene(person, boss, tenant, eventId, [.. ids]);
    }

    async Task<List<BsonDocument>> StoredAsync(Guid eventId)
    {
        var filter = new BsonDocument("EventId", EventSeed.Binary(eventId));
        return await RegistrySeed.Collection(_mongo.ConnectionString, "event_handouts").Find(filter).ToListAsync();
    }

    /// <summary>A Participation with its one Phase, the last, that starts at six in the morning.</summary>
    static Func<Guid, Participation> Plain(int number)
    {
        return eventId =>
            IntegrationPayloadFactory.ActiveParticipation(eventId, number, Guid.NewGuid(), startTime: START);
    }

    /// <summary>A Participation of two Phases, of which the first starts at six in the morning, and which has arrived in it when asked to have.</summary>
    static Func<Guid, Participation> TwoPhases(int number, bool arrived = false)
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
                null,
                CompetitionRuleset.FEI,
                false,
                null,
                Timestamp.Create(START),
                arrived ? Timestamp.Create(START.AddHours(3)) : null,
                arrived ? Timestamp.Create(START.AddHours(3.25)) : null,
                null,
                false,
                false,
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
                arrived ? Timestamp.Create(START.AddHours(3.25)) : null,
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

    sealed class Scene
    {
        public Scene(Person person, Person boss, string tenant, Guid eventId, Guid[] ids)
        {
            Person = person;
            Boss = boss;
            Tenant = tenant;
            EventId = eventId;
            Ids = ids;
        }

        public Person Person { get; }
        public Person Boss { get; }
        public string Tenant { get; }
        public Guid EventId { get; }
        public Guid[] Ids { get; }
    }

    /// <summary>One tab of the Main Operator: its own store of the Participations, its dashboard and its Handouts, over the Api.</summary>
    sealed class Tab : IAsyncDisposable
    {
        public static async Task<Tab> OpenAsync(
            ApiFactory api,
            Scene scene,
            Person? person = null,
            bool canWrite = true
        )
        {
            var json = JsonApiClients.Of(api, person ?? scene.Person, out var requests);
            var selected = new SelectedEvent(scene.EventId);
            var store = new ParticipationStore(
                new ParticipationEventScopedApiRepository(json, new EventScopeFactory<Participation>(selected))
            );
            var view = new StubViewedEvent(store, selected.Event!, canWrite);
            var dashboard = new ConsoleDashboard(
                store,
                new ConsoleParticipationWrites(json),
                new ServerClock(json, TimeProvider.System),
                new AccountSession(json),
                new SnapshotApiPublisher(json)
            );
            await dashboard.Show(view);
            var notifier = new Notifier();
            var tab = new Tab(
                dashboard,
                new ConsoleHandouts(store, new ConsoleHandoutWrites(json), notifier),
                store,
                view,
                requests
            );
            notifier.Informed.Subscribe(tab.Informed.Add);
            notifier.Warned.Subscribe(tab.Warned.Add);
            await tab.Handouts.Show(view);
            return tab;
        }

        Tab(
            ConsoleDashboard dashboard,
            ConsoleHandouts handouts,
            ParticipationStore store,
            StubViewedEvent view,
            JsonApiClients.Requests requests
        )
        {
            Dashboard = dashboard;
            Handouts = handouts;
            Store = store;
            View = view;
            Requests = requests;
        }

        public ConsoleDashboard Dashboard { get; }
        public ConsoleHandouts Handouts { get; }
        public ParticipationStore Store { get; }
        public StubViewedEvent View { get; }
        public JsonApiClients.Requests Requests { get; }
        public List<string> Informed { get; } = [];
        public List<string> Warned { get; } = [];

        public ValueTask DisposeAsync()
        {
            Handouts.Dispose();
            Dashboard.Dispose();
            Store.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
