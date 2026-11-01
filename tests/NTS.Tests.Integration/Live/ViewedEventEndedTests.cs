using Microsoft.Extensions.Time.Testing;
using Not.Domain.Exceptions;
using Not.Events;
using Not.Krud.Abstractions;
using Not.Notify;
using NoTiming.Api.Features.Reference;
using NoTiming.Ui.Features.Account;
using NoTiming.Ui.Features.Console;
using NoTiming.Ui.Features.Core;
using NoTiming.Ui.Features.Core.Dashboard;
using NoTiming.Ui.Features.Core.Participations;
using NoTiming.Ui.Features.Sessions;
using NoTiming.Ui.Storage.Core.Repositories;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Console;
using NTS.Contracts.Features.Console.Rankings;
using NTS.Domain.Access;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Tests.Integration.Harness.Drivers;
using NTS.Tests.Integration.Harness;
using static NTS.Tests.Integration.Harness.TenancySeed;
using Not.Blazor.Client.Browser;
using NoTiming.Ui.Storage.Browser;

namespace NTS.Tests.Integration;

/// <summary>
/// What a write that the Api refuses tells the view the person works in (#631, ADR-0007): the Snapshot page's service and the
/// services of the Console read the code of a refusal and hand it to the view, which says once that the Event has ended when the
/// code says so. The Api's clock is the test's: the Event is Live until the test moves it past the last day, and the refusal is
/// the Api's own. Over the real Api in this process, with the services built as the app builds them.
/// </summary>
public sealed class ViewedEventEndedTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset START = new(DateTime.Today.AddHours(6));
    static readonly DateTimeOffset ARRIVE = START.AddMinutes(30);

    readonly MongoFixture _mongo;

    public ViewedEventEndedTests(MongoFixture mongo)
    {
        ApiMongo.Configure();
        _mongo = mongo;
    }

    [Fact]
    public async Task A_Snapshot_that_the_Api_refuses_because_the_Event_has_ended_is_said_in_its_words_and_told_to_the_view()
    {
        await using var scene = await SceneAsync();
        await using var page = await SnapshotPage.OpenAsync(scene, scene.Official);
        Select(page, ARRIVE);
        scene.EndTheEvent();

        var refused = await Assert.ThrowsAsync<DomainException>(
            () => page.Service.Publish(page.View, SnapshotType.Arrive)
        );

        Assert.Contains("ended", refused.Message, StringComparison.OrdinalIgnoreCase); // the words of the Api, and not the code
        Assert.Equal(["event-ended"], page.View.Refusals);
    }

    [Fact]
    public async Task A_correction_and_a_group_sent_again_that_the_Api_refuses_because_the_Event_has_ended_are_told_to_the_view_too()
    {
        await using var scene = await SceneAsync();
        await using var page = await SnapshotPage.OpenAsync(scene, scene.Official);
        Select(page, ARRIVE);
        Assert.True(await page.Service.Publish(page.View, SnapshotType.Arrive));
        var group = Assert.Single(page.Service.History);
        var entry = Assert.Single(group.Entries);
        scene.EndTheEvent();

        await Assert.ThrowsAsync<DomainException>(
            () => page.Service.UpdateTime(page.View, group, entry, new Timestamp(ARRIVE.AddMinutes(2)))
        );
        await Assert.ThrowsAsync<DomainException>(() => page.Service.RePublish(page.View, group, SnapshotType.Arrive));

        Assert.Equal(["event-ended", "event-ended"], page.View.Refusals);
    }

    [Fact]
    public async Task A_Snapshot_that_the_Api_refuses_for_another_reason_is_told_to_the_view_with_its_own_code()
    {
        await using var scene = await SceneAsync();
        await using var page = await SnapshotPage.OpenAsync(scene, scene.Stranger); // who is not an Official of the Event
        Select(page, ARRIVE);

        await Assert.ThrowsAsync<DomainException>(() => page.Service.Publish(page.View, SnapshotType.Arrive));

        Assert.Equal(["not-allowed"], page.View.Refusals);
    }

    [Fact]
    public async Task A_manual_edit_that_the_Api_refuses_because_the_Event_has_ended_is_said_in_its_words_and_told_to_the_view()
    {
        await using var scene = await SceneAsync();
        await using var tab = await ConsoleTab.OpenAsync(scene);
        scene.EndTheEvent();

        var refused = await Assert.ThrowsAsync<DomainException>(() => tab.Dashboard.Withdraw());

        Assert.Contains("ended", refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["event-ended"], tab.View.Refusals);
    }

    [Fact]
    public async Task A_time_that_the_Console_Operator_records_after_the_Event_ended_is_told_to_the_view()
    {
        await using var scene = await SceneAsync();
        await using var tab = await ConsoleTab.OpenAsync(scene);
        scene.EndTheEvent();

        await Assert.ThrowsAsync<DomainException>(
            () => tab.Dashboard.RecordTime(SnapshotType.Arrive, TimeSpan.FromHours(7))
        );

        Assert.Equal(["event-ended"], tab.View.Refusals);
    }

    [Fact]
    public async Task An_edit_over_a_Participation_that_another_tab_changed_is_told_to_the_view_with_its_own_code()
    {
        await using var scene = await SceneAsync();
        await using var tab = await ConsoleTab.OpenAsync(scene);
        await using var other = await ConsoleTab.OpenAsync(scene);
        await other.Dashboard.Withdraw();
        tab.View.Refusals.Clear();

        await Assert.ThrowsAsync<DomainException>(() => tab.Dashboard.Retire());

        Assert.Equal(["participation-changed"], tab.View.Refusals);
    }

    [Fact]
    public async Task A_Ranking_that_the_Console_Operator_makes_or_removes_after_the_Event_ended_is_told_to_the_view()
    {
        await using var scene = await SceneAsync();
        await using var tab = await ConsoleTab.OpenAsync(scene);
        var model = new CustomRankingModel
        {
            Name = "Open class",
            Ruleset = CompetitionRuleset.FEI,
            Category = ParticipationCategory.Senior,
            Entries = [new CustomRankingEntryModel { Participation = tab.Store.Participations.First() }],
        };
        await ((IKrudFormService<CustomRankingModel>)tab.Rankings).Create(model);
        tab.View.Refusals.Clear();
        scene.EndTheEvent();

        await Assert.ThrowsAsync<DomainException>(
            () => ((IKrudFormService<CustomRankingModel>)tab.Rankings).Create(model)
        );
        await Assert.ThrowsAsync<DomainException>(() => tab.Rankings.Delete(model.MapToEntity()));

        Assert.Equal(["event-ended", "event-ended"], tab.View.Refusals);
    }

    [Fact]
    public async Task A_Handout_that_the_Console_Operator_makes_or_removes_after_the_Event_ended_is_told_to_the_view()
    {
        await using var scene = await SceneAsync();
        await using var tab = await ConsoleTab.OpenAsync(scene);
        await tab.Handouts.Create(1);
        var made = Assert.Single(tab.Handouts.Documents);
        tab.View.Refusals.Clear();
        scene.EndTheEvent();

        await Assert.ThrowsAsync<DomainException>(() => tab.Handouts.Create(2));
        Assert.Equal(["event-ended"], tab.View.Refusals); // the one that was to be made
        tab.View.Refusals.Clear();
        await Assert.ThrowsAsync<DomainException>(() => tab.Handouts.Delete([made]));

        Assert.Equal(["event-ended"], tab.View.Refusals); // and the one that was to be removed
    }

    [Fact]
    public async Task The_Console_lists_an_Event_that_has_ended_as_a_Historic_one_when_it_reads_the_Events_again_and_not_before()
    {
        await using var scene = await SceneAsync();
        await using var ui = ConsoleUi.Open(scene.Api, scene.ConsoleOperator);
        var events = ui.Get<IConsoleEvents>();
        await events.Load();
        Assert.Equal(EventStage.Live, events.Events.Single(x => x.Id == scene.EventId).Stage);
        scene.EndTheEvent();

        Assert.Equal(EventStage.Live, events.Events.Single(x => x.Id == scene.EventId).Stage); // nothing tells it
        await events.Refresh();

        Assert.Equal(EventStage.Historic, events.Events.Single(x => x.Id == scene.EventId).Stage);
    }

    static void Select(SnapshotPage page, DateTimeOffset time)
    {
        page.Service.SelectForSnapshot(page.Service.Participations.Single(x => x.Combination.Number == 1));
        page.Service.UpdateTimestamp(page.Service.Snapshots.Single(), new Timestamp(time));
    }

    async Task<Scene> SceneAsync()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var api = new ApiFactory(_mongo.ConnectionString, time: clock);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var eventId = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, consoleOperator.Id, clock.GetUtcNow());
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
        var stranger = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        foreach (var number in new[] { 1, 2 })
        {
            var participation = IntegrationPayloadFactory.ActiveParticipation(
                eventId,
                number,
                Guid.NewGuid(),
                startTime: START
            );
            await EventSeed.ParticipationAsync(_mongo.ConnectionString, tenant, participation);
        }

        return new Scene(api, clock, consoleOperator, official, stranger, eventId);
    }

    sealed class Scene : IAsyncDisposable
    {
        readonly ApiFactory _api;
        readonly FakeTimeProvider _clock;

        public Scene(
            ApiFactory api,
            FakeTimeProvider clock,
            Person consoleOperator,
            Person official,
            Person stranger,
            Guid eventId
        )
        {
            _api = api;
            _clock = clock;
            ConsoleOperator = consoleOperator;
            Official = official;
            Stranger = stranger;
            EventId = eventId;
        }

        public ApiFactory Api => _api;
        public Person ConsoleOperator { get; }
        public Person Official { get; }
        public Person Stranger { get; }
        public Guid EventId { get; }

        /// <summary>The clock of the Api moves past the last day of the Event, which is a day after it started.</summary>
        public void EndTheEvent()
        {
            _clock.Advance(TimeSpan.FromDays(3));
        }

        public ValueTask DisposeAsync()
        {
            return _api.DisposeAsync();
        }
    }

    /// <summary>The Snapshot page of a person: the service it calls, over the stores and the clients of the Ui.</summary>
    sealed class SnapshotPage : IAsyncDisposable
    {
        public static async Task<SnapshotPage> OpenAsync(Scene scene, Person person)
        {
            var json = JsonApiClients.Of(scene.Api, person, out _);
            var selected = new SelectedEvent(scene.EventId);
            var store = new ParticipationStore(
                new ParticipationEventScopedApiRepository(json, new EventScopeFactory<Participation>(selected))
            );
            var account = new AccountSession(json);
            var sessions = new WitnessUserSessionService(account, json);
            await sessions.SetEventId(scene.EventId);
            var service = new SnapshotService(
                selected,
                store,
                sessions,
                new SnapshotApiPublisher(json),
                account,
                new BrowserDurableRepository<DurableSnapshotGroup>(
                    new BrowserLocalStorage(new MemoryLocalStorage()),
                    new TaskDelay(),
                    DurableSnapshotGroup.KEY_PREFIX,
                    DurableSnapshotGroup.Options
                ),
                new Quiet()
            );
            await service.Load();
            return new SnapshotPage(service, store, new TellingView(store, selected.Event!));
        }

        SnapshotPage(SnapshotService service, ParticipationStore store, TellingView view)
        {
            Service = service;
            Store = store;
            View = view;
        }

        public SnapshotService Service { get; }
        public ParticipationStore Store { get; }
        public TellingView View { get; }

        public ValueTask DisposeAsync()
        {
            Service.Dispose();
            Store.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>The Console of the Console Operator: the services the dashboard, the Rankings and the Handouts call, over one view.</summary>
    sealed class ConsoleTab : IAsyncDisposable
    {
        public static async Task<ConsoleTab> OpenAsync(Scene scene)
        {
            var json = JsonApiClients.Of(scene.Api, scene.ConsoleOperator, out _);
            var selected = new SelectedEvent(scene.EventId);
            var store = new ParticipationStore(
                new ParticipationEventScopedApiRepository(json, new EventScopeFactory<Participation>(selected))
            );
            var view = new TellingView(store, selected.Event!);
            var dashboard = new ConsoleDashboard(
                store,
                new ConsoleParticipationWrites(json),
                new ServerClock(json, TimeProvider.System),
                new AccountSession(json),
                new SnapshotApiPublisher(json)
            );
            await dashboard.Show(view);
            dashboard.Selected = dashboard.Participations.First();
            var rankings = new ConsoleRankings(new ConsoleRankingWrites(json));
            rankings.Show(view);
            var handouts = new ConsoleHandouts(store, new ConsoleHandoutWrites(json), new Notifier());
            await handouts.Show(view);
            return new ConsoleTab(dashboard, rankings, handouts, store, view);
        }

        ConsoleTab(
            ConsoleDashboard dashboard,
            ConsoleRankings rankings,
            ConsoleHandouts handouts,
            ParticipationStore store,
            TellingView view
        )
        {
            Dashboard = dashboard;
            Rankings = rankings;
            Handouts = handouts;
            Store = store;
            View = view;
        }

        public ConsoleDashboard Dashboard { get; }
        public ConsoleRankings Rankings { get; }
        public ConsoleHandouts Handouts { get; }
        public ParticipationStore Store { get; }
        public TellingView View { get; }

        public ValueTask DisposeAsync()
        {
            Handouts.Dispose();
            Dashboard.Dispose();
            Store.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>A view that says what it was told of the refusals that the Api made, and nothing more.</summary>
    sealed class TellingView : IViewedEvent
    {
        readonly StubViewedEvent _inner;

        public TellingView(IParticipationStore store, EventInformation @event)
        {
            _inner = new StubViewedEvent(store, @event);
        }

        /// <summary>The codes of the refusals the services told the view, in order.</summary>
        public List<string?> Refusals { get; } = [];

        public IEventSubscriber ObservableEvent => _inner.ObservableEvent;
        public EventInformation Event => _inner.Event;
        public EventStage Stage => _inner.Stage;
        public bool IsLive => _inner.IsLive;
        public bool CanWrite => _inner.CanWrite;
        public IReadOnlyList<Participation> Participations => _inner.Participations;
        public IReadOnlyList<Ranking> Rankings => _inner.Rankings;
        public IReadOnlyList<Official> Officials => _inner.Officials;

        public Task Load()
        {
            return _inner.Load();
        }

        public Participation? Find(Guid id)
        {
            return _inner.Find(id);
        }

        public Task Refresh()
        {
            return _inner.Refresh();
        }

        public ResultsDocument CreateDocument(Ranking ranking)
        {
            return _inner.CreateDocument(ranking);
        }

        public bool Shows(CoreView view)
        {
            return _inner.Shows(view);
        }

        public void EnsureCanWrite()
        {
            _inner.EnsureCanWrite();
        }

        public void Refused(string? code)
        {
            Refusals.Add(code);
        }

        public void Dispose()
        {
            _inner.Dispose();
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
