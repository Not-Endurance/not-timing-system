using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Application.RPC;
using Not.Domain;
using Not.Events;
using NoTiming.Ui.Features.Core.EventViews;
using NTS.Application.Core;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Access;
using NTS.Contracts.Socket;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using Country = NTS.Domain.Aggregates.Country;

namespace NTS.Tests.Unit.Application;

/// <summary>What the tests of the viewed Event stand behind: the Api's reads, the connection and the access of the person.</summary>
internal static class ViewedEventFixtures
{
    public static readonly Guid HISTORIC_EVENT_ID = TestId.Of(1);
    public static readonly Guid LIVE_EVENT_ID = FakeSocketContext.EVENT_ID;
    public static readonly Guid OTHER_LIVE_EVENT_ID = TestId.Of(200);

    public static EventInformation EventOf(Guid id)
    {
        return new EventInformation(
            new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG"),
            "Event",
            "Location",
            new EventSpan(DateTimeOffset.Now.Date.AddDays(-1), DateTimeOffset.Now.Date),
            null,
            id: id
        );
    }

    /// <summary>The Live Event the app follows, opened, as a person of that access level sees it.</summary>
    public static async Task<IViewedEvent> LiveView(IParticipationStore store, WitnessAccessLevel level)
    {
        var view = new LiveEventView(
            EventOf(LIVE_EVENT_ID),
            store,
            new AccessOf(level),
            new ReadOnlyRepository<Ranking>([]),
            new ReadOnlyRepository<Official>([])
        );
        await view.Load();
        return view;
    }

    /// <summary>An Event that has ended, opened, as a record, which nobody can write to.</summary>
    public static async Task<IViewedEvent> HistoricView()
    {
        var view = new HistoricEventView(
            EventOf(HISTORIC_EVENT_ID),
            new ReadOnlyRepository<Participation>([]),
            new ReadOnlyRepository<Ranking>([]),
            new ReadOnlyRepository<Official>([])
        );
        await view.Load();
        return view;
    }

    public static Ranking Ranking(Guid eventId, params RankingEntry[] entries)
    {
        return new Ranking(
            "CEI 1*",
            CompetitionRuleset.Regional,
            ParticipationCategory.Senior,
            null,
            null,
            null,
            null,
            null,
            entries,
            eventId
        );
    }
}

/// <summary>Reads what it holds and writes nothing: a write that reaches it is a test that failed.</summary>
internal class ReadOnlyRepository<T> : IRepository<T>
    where T : Entity
{
    readonly List<T> _items;

    public ReadOnlyRepository(IEnumerable<T> items)
    {
        _items = [.. items];
    }

    /// <summary>The reads that were made, of one or of many.</summary>
    public int Reads { get; private set; }

    public Task<T?> Read(Guid id)
    {
        Reads++;
        return Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
    }

    public Task<T?> Read(Expression<Func<T, bool>> filter)
    {
        Reads++;
        return Task.FromResult(_items.AsQueryable().FirstOrDefault(filter));
    }

    public Task<IEnumerable<T>> ReadMany()
    {
        Reads++;
        return Task.FromResult<IEnumerable<T>>(_items.ToArray());
    }

    public Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
    {
        Reads++;
        return Task.FromResult<IEnumerable<T>>(_items.AsQueryable().Where(filter).ToArray());
    }

    public Task Create(T item)
    {
        throw new NotSupportedException("The repository only reads.");
    }

    public Task Update(T item)
    {
        throw new NotSupportedException("The repository only reads.");
    }

    public Task Delete(T item)
    {
        throw new NotSupportedException("The repository only reads.");
    }

    public Task DeleteMany(IEnumerable<T> items)
    {
        throw new NotSupportedException("The repository only reads.");
    }

    public Task DeleteMany(Expression<Func<T, bool>> filter)
    {
        throw new NotSupportedException("The repository only reads.");
    }
}

/// <summary>The Events the Api lists as Live and as Historic, which is its rule and its clock.</summary>
internal sealed class EventsOf : ReadOnlyRepository<EventInformation>, IEventInformationRepository
{
    readonly List<EventInformation> _live;
    readonly List<EventInformation> _historic;

    public EventsOf(IEnumerable<EventInformation> live, IEnumerable<EventInformation> historic)
        : base([.. live, .. historic])
    {
        _live = [.. live];
        _historic = [.. historic];
    }

    public Task<IEnumerable<EventInformation>> ReadLive()
    {
        return Task.FromResult<IEnumerable<EventInformation>>(_live.ToArray());
    }

    public Task<IEnumerable<EventInformation>> ReadHistoric()
    {
        return Task.FromResult<IEnumerable<EventInformation>>(_historic.ToArray());
    }

    public Task<EventInformation> Start(Guid configureEventId)
    {
        throw new NotSupportedException("The views read Events only.");
    }

    public Task Reset()
    {
        throw new NotSupportedException("The views read Events only.");
    }
}

/// <summary>
/// The connection of the app as the provider drives it: it records what was asked of it, and connecting makes the Event
/// the connected one and tells whoever the test says is listening, as the real socket service does through the domain
/// events.
/// </summary>
internal sealed class RecordingSocketService : INtsSocketService
{
    readonly Event _changed = new();

    public List<Guid> Connects { get; } = [];
    public int Disconnects { get; private set; }

    /// <summary>What the app does when an Event is connected: the handlers of <c>EventConnected</c>.</summary>
    public Func<EventInformation, Task>? OnConnected { get; set; }

    public bool IsConnected => Event != null;
    public SocketConnectionStatus Status =>
        IsConnected ? SocketConnectionStatus.Connected : SocketConnectionStatus.Disconnected;
    public EventInformation? Event { get; private set; }
    public IEventSubscriber ObservableEvent => _changed;

    public Task Load()
    {
        return Task.CompletedTask;
    }

    public void ConnectedTo(EventInformation eventInformation)
    {
        Event = eventInformation;
    }

    public async Task Connect(EventInformation eventInformation)
    {
        Connects.Add(eventInformation.Id);
        Event = eventInformation;
        if (OnConnected != null)
        {
            await OnConnected(eventInformation);
        }

        _changed.Emit();
    }

    public Task Disconnect()
    {
        Disconnects++;
        Event = null;
        return Task.CompletedTask;
    }

    public Task<bool> WillResetSession(EventInformation eventInformation)
    {
        return Task.FromResult(false);
    }
}

/// <summary>
/// What the Api said the person may do about the Event they follow. As the real context does, it knows nothing until it is
/// loaded: the level is the Api's answer, which a service asks for when it loads.
/// </summary>
internal sealed class AccessOf : IWitnessAccessContext
{
    readonly Event _changed = new();
    WitnessAccessLevel _answer;

    public AccessOf(WitnessAccessLevel level)
    {
        _answer = level;
    }

    public WitnessAccessLevel AccessLevel { get; private set; } = WitnessAccessLevel.Unknown;
    public IEventSubscriber ObservableEvent => _changed;

    public Task Load()
    {
        AccessLevel = _answer;
        return Task.CompletedTask;
    }

    public void Become(WitnessAccessLevel level)
    {
        _answer = level;
        AccessLevel = level;
        _changed.Emit();
    }
}
