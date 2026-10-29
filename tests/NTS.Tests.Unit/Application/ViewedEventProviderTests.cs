using Not.Domain.Exceptions;
using NoTiming.Ui.Features.Core.EventViews;
using NoTiming.Ui.Features.Core.Participations;
using NTS.Contracts.Features.Access;
using NTS.Domain.Access;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Enums;
using static NTS.Tests.Unit.Application.ViewedEventFixtures;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// The Event a Core view shows is opened by its id and answers by the stage it is in (#630, ADR-0007): a Live Event is read
/// through the connection and the store the live views already use, a Historic Event is read by its id with no connection
/// and nothing that the live services share, and a Historic Event takes no write from a view whoever looks at it. The
/// Results of its Rankings are composed from the Participations it names (#624, ADR-0006).
/// </summary>
public sealed class ViewedEventProviderTests
{
    static readonly Guid OTHER_EVENT_ID = TestId.Of(300);

    [Fact]
    public async Task A_Historic_Event_is_read_by_its_id_and_holds_what_that_Event_keeps_and_nothing_of_another()
    {
        var mine = ParticipationFixtures.Completed(1);
        var theirs = ParticipationFixtures.CompletedIn(OTHER_EVENT_ID, 9);
        var myRanking = Ranking(HISTORIC_EVENT_ID, new RankingEntry(mine.Id, false));
        var theirRanking = Ranking(OTHER_EVENT_ID, new RankingEntry(theirs.Id, false));
        var myOfficial = new Official("Anna", null, OfficialRole.Steward, HISTORIC_EVENT_ID);
        var theirOfficial = new Official("Boris", null, OfficialRole.Steward, OTHER_EVENT_ID);
        var fixture = Open(
            historic: [EventOf(HISTORIC_EVENT_ID)],
            participations: [mine, theirs],
            rankings: [myRanking, theirRanking],
            officials: [myOfficial, theirOfficial]
        );

        var view = await fixture.Provider.Open(HISTORIC_EVENT_ID);

        Assert.NotNull(view);
        Assert.Equal(HISTORIC_EVENT_ID, view.Event.Id);
        Assert.Equal(EventStage.Historic, view.Stage);
        Assert.False(view.IsLive);
        Assert.Equal([mine.Id], view.Participations.Select(x => x.Id));
        Assert.Equal([myRanking.Id], view.Rankings.Select(x => x.Id));
        Assert.Equal([myOfficial.Id], view.Officials.Select(x => x.Id));
        Assert.Same(mine, view.Find(mine.Id));
        Assert.Null(view.Find(theirs.Id));
    }

    [Fact]
    public async Task The_document_of_a_Ranking_is_composed_from_the_Participations_it_names()
    {
        var arrive = DateTimeOffset.Now.AddHours(-3);
        var first = ParticipationFixtures.CompletedAt(1, arrive);
        var second = ParticipationFixtures.CompletedAt(2, arrive.AddMinutes(30));
        var ranking = Ranking(HISTORIC_EVENT_ID, new RankingEntry(second.Id, false), new RankingEntry(first.Id, false));
        var fixture = Open(
            historic: [EventOf(HISTORIC_EVENT_ID)],
            participations: [first, second],
            rankings: [ranking]
        );

        var view = await fixture.Provider.Open(HISTORIC_EVENT_ID);

        var document = Assert.IsType<ResultsDocument>(view!.CreateDocument(ranking));
        Assert.Equal([first.Id, second.Id], document.Entries.Select(x => x.ParticipationId));
        Assert.Equal([1, 2], document.Entries.Select(x => x.Rank));
        Assert.Same(first, document.Entries[0].Participation);
    }

    [Fact]
    public async Task A_Participation_in_two_Rankings_is_marked_in_each_by_that_Ranking_alone()
    {
        var arrive = DateTimeOffset.Now.AddHours(-3);
        var first = ParticipationFixtures.CompletedAt(1, arrive);
        var second = ParticipationFixtures.CompletedAt(2, arrive.AddMinutes(30));
        var regular = Ranking(HISTORIC_EVENT_ID, new RankingEntry(first.Id, false), new RankingEntry(second.Id, false));
        var custom = Ranking(HISTORIC_EVENT_ID, new RankingEntry(first.Id, true), new RankingEntry(second.Id, false));
        var fixture = Open(
            historic: [EventOf(HISTORIC_EVENT_ID)],
            participations: [first, second],
            rankings: [regular, custom]
        );
        var view = await fixture.Provider.Open(HISTORIC_EVENT_ID);

        var inTheRegularOne = view!.CreateDocument(regular).Entries.Select(x => x.ParticipationId).ToList();
        var inTheCustomOne = view.CreateDocument(custom).Entries.Select(x => x.ParticipationId).ToList();

        Assert.Equal([first.Id, second.Id], inTheRegularOne);
        Assert.Equal([second.Id, first.Id], inTheCustomOne);
    }

    [Fact]
    public async Task Opening_a_Historic_Event_leaves_the_connection_and_what_the_Live_Event_holds_untouched()
    {
        var fixture = Open(
            live: [EventOf(LIVE_EVENT_ID)],
            historic: [EventOf(HISTORIC_EVENT_ID)],
            participations: [ParticipationFixtures.Completed(1)],
            liveStore: [ParticipationFixtures.Active(5), ParticipationFixtures.Active(6)]
        );
        await fixture.Store.Load();
        var held = fixture.Store.Participations;

        var view = await fixture.Provider.Open(HISTORIC_EVENT_ID);

        Assert.Equal(EventStage.Historic, view!.Stage);
        Assert.Empty(fixture.Socket.Connects);
        Assert.Equal(0, fixture.Socket.Disconnects);
        Assert.Equal(LIVE_EVENT_ID, fixture.Socket.Event?.Id);
        Assert.Same(held, fixture.Store.Participations);
        Assert.Equal([5, 6], fixture.Store.Participations.Select(x => x.Combination.Number));
    }

    [Fact]
    public async Task A_Historic_Event_is_a_record_that_a_change_notification_of_its_Participations_does_not_move()
    {
        var participation = ParticipationFixtures.Completed(1);
        var fixture = Open(historic: [EventOf(HISTORIC_EVENT_ID)], participations: [participation]);
        var view = await fixture.Provider.Open(HISTORIC_EVENT_ID);
        var changes = 0;
        view!.ObservableEvent.Subscribe(() => changes++);

        await fixture.Store.Handle(
            new ParticipationChanged(HISTORIC_EVENT_ID, participation.Id),
            CancellationToken.None
        );

        Assert.Equal(0, changes);
        Assert.Same(participation, view.Find(participation.Id));
    }

    [Theory]
    [InlineData(WitnessAccessLevel.Anonymous)]
    [InlineData(WitnessAccessLevel.Registered)]
    [InlineData(WitnessAccessLevel.Official)]
    public async Task A_Historic_Event_takes_no_write_from_a_view_whoever_looks_at_it(WitnessAccessLevel level)
    {
        var fixture = Open(historic: [EventOf(HISTORIC_EVENT_ID)], access: level);
        var view = await fixture.Provider.Open(HISTORIC_EVENT_ID);

        Assert.False(view!.CanWrite);
        Assert.Throws<DomainException>(view.EnsureCanWrite);
    }

    [Fact]
    public async Task A_Live_Event_is_the_connected_one_and_follows_what_its_store_holds()
    {
        var fixture = Open(live: [EventOf(LIVE_EVENT_ID)], liveStore: [ParticipationFixtures.Active(1)]);
        await fixture.Store.Load();

        var view = await fixture.Provider.Open(LIVE_EVENT_ID);

        Assert.Equal(EventStage.Live, view!.Stage);
        Assert.True(view.IsLive);
        Assert.Empty(fixture.Socket.Connects); // the app follows it already
        Assert.Equal([1], view.Participations.Select(x => x.Combination.Number));
        var changes = 0;
        view.ObservableEvent.Subscribe(() => changes++);

        var arrived = ParticipationFixtures.Active(2);
        fixture.Repository.Store(arrived);
        await fixture.Store.Handle(ParticipationFixtures.Changed(arrived), CancellationToken.None);

        Assert.Equal([1, 2], view.Participations.Select(x => x.Combination.Number));
        Assert.Same(arrived, view.Find(arrived.Id));
        Assert.True(changes > 0);
    }

    [Fact]
    public async Task A_Live_Event_that_is_not_the_connected_one_replaces_the_connection_and_shows_its_own_Participations()
    {
        var fixture = Open(
            live: [EventOf(LIVE_EVENT_ID), EventOf(OTHER_LIVE_EVENT_ID)],
            liveStore: [ParticipationFixtures.Active(1)]
        );
        await fixture.Store.Load();
        fixture.Socket.OnConnected = async info =>
        {
            fixture.Repository.Remove(TestId.Of(1));
            fixture.Repository.Store(ParticipationFixtures.Active(7));
            await fixture.Store.Handle(new EventConnected(info.Id), CancellationToken.None);
        };

        var view = await fixture.Provider.Open(OTHER_LIVE_EVENT_ID);

        Assert.Equal([OTHER_LIVE_EVENT_ID], fixture.Socket.Connects);
        Assert.Equal(OTHER_LIVE_EVENT_ID, view!.Event.Id);
        Assert.Equal([7], view.Participations.Select(x => x.Combination.Number));
    }

    [Theory]
    [InlineData(WitnessAccessLevel.Anonymous, false)]
    [InlineData(WitnessAccessLevel.Registered, false)]
    [InlineData(WitnessAccessLevel.Official, true)]
    public async Task A_Live_Event_may_be_written_to_by_an_Official_and_by_no_one_else(
        WitnessAccessLevel level,
        bool canWrite
    )
    {
        var fixture = Open(live: [EventOf(LIVE_EVENT_ID)], access: level);
        var view = await fixture.Provider.Open(LIVE_EVENT_ID);

        Assert.Equal(canWrite, view!.CanWrite);
        if (canWrite)
        {
            view.EnsureCanWrite();
        }
        else
        {
            Assert.Throws<DomainException>(view.EnsureCanWrite);
        }
    }

    [Fact]
    public async Task What_the_person_may_do_is_followed_when_they_sign_in()
    {
        var fixture = Open(live: [EventOf(LIVE_EVENT_ID)], access: WitnessAccessLevel.Anonymous);
        var view = await fixture.Provider.Open(LIVE_EVENT_ID);
        var changes = 0;
        view!.ObservableEvent.Subscribe(() => changes++);
        Assert.False(view.CanWrite);

        fixture.Access.Become(WitnessAccessLevel.Official);

        Assert.True(view.CanWrite);
        Assert.True(changes > 0);
    }

    [Fact]
    public async Task An_Event_that_is_neither_Live_nor_Historic_is_not_opened()
    {
        var fixture = Open(live: [EventOf(LIVE_EVENT_ID)], historic: [EventOf(HISTORIC_EVENT_ID)]);

        Assert.Null(await fixture.Provider.Open(TestId.Of(999)));
    }

    [Fact]
    public async Task A_view_shows_the_Core_views_its_stage_allows()
    {
        var fixture = Open(live: [EventOf(LIVE_EVENT_ID)], historic: [EventOf(HISTORIC_EVENT_ID)]);

        var live = await fixture.Provider.Open(LIVE_EVENT_ID);
        var historic = await fixture.Provider.Open(HISTORIC_EVENT_ID);

        Assert.All(Enum.GetValues<CoreView>(), view => Assert.True(live!.Shows(view)));
        Assert.True(historic!.Shows(CoreView.Results));
        Assert.True(historic.Shows(CoreView.Rankings));
        Assert.False(historic.Shows(CoreView.Startlist));
        Assert.False(historic.Shows(CoreView.Arrivelist));
        Assert.False(historic.Shows(CoreView.SnapshotCapture));
        Assert.False(historic.Shows(CoreView.Handouts));
    }

    /// <summary>
    /// The app as the provider finds it: following the Live Event (which has the Participations of the live store), with the
    /// Api's reads of the Events, and the rows of the Events it keeps.
    /// </summary>
    static Fixture Open(
        EventInformation[]? live = null,
        EventInformation[]? historic = null,
        Participation[]? participations = null,
        Ranking[]? rankings = null,
        Official[]? officials = null,
        Participation[]? liveStore = null,
        WitnessAccessLevel access = WitnessAccessLevel.Anonymous
    )
    {
        var socket = new RecordingSocketService();
        var repository = new ControlledParticipationRepository(liveStore ?? []);
        var store = new ParticipationStore(repository, socket);
        socket.ConnectedTo(EventOf(LIVE_EVENT_ID));
        socket.OnConnected = info => store.Handle(new EventConnected(info.Id), CancellationToken.None);
        var accessContext = new AccessOf(access);
        var provider = new ViewedEventProvider(
            new EventsOf(live ?? [], historic ?? []),
            new ReadOnlyRepository<Participation>(participations ?? []),
            new ReadOnlyRepository<Ranking>(rankings ?? []),
            new ReadOnlyRepository<Official>(officials ?? []),
            socket,
            store,
            accessContext
        );
        return new Fixture(provider, socket, store, repository, accessContext);
    }

    sealed class Fixture
    {
        public Fixture(
            ViewedEventProvider provider,
            RecordingSocketService socket,
            ParticipationStore store,
            ControlledParticipationRepository repository,
            AccessOf access
        )
        {
            Provider = provider;
            Socket = socket;
            Store = store;
            Repository = repository;
            Access = access;
        }

        public ViewedEventProvider Provider { get; }
        public RecordingSocketService Socket { get; }
        public ParticipationStore Store { get; }
        public ControlledParticipationRepository Repository { get; }
        public AccessOf Access { get; }
    }
}
