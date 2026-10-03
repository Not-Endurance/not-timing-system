using MediatR;
using Not.Application.Behinds.Adapters;
using Not.Injection;
using NTS.Contracts.Core;
using NTS.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;

namespace NoTiming.Ui.Features.Core.Participations;

/// <summary>
/// The one owner of the Event's Participations in the Ui (ADR-0006). It loads all of them when the Event connects,
/// empties when it is left, and keeps them current by reading again the Participation a change notification names,
/// never by what a notification carries. At most one read per Participation is in flight; notifications that arrive
/// during it cost one more read, once. Every list and page is a view over it.
/// </summary>
public sealed class ParticipationStore
    : NStatefulService,
        IParticipationStore,
        INotificationHandler<ParticipationChanged>,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>,
        IScoped
{
    readonly IEventScopedRepository<Participation> _participations;
    readonly INtsSocketContext? _socketContext;
    readonly object _lock = new();
    readonly Dictionary<Guid, Reading> _readings = [];
    readonly HashSet<Guid> _refreshedWhileLoading = [];
    int _connection; // counts the connections: what a read of an earlier one brings back is dropped
    bool _loading;

    public ParticipationStore(IEventScopedRepository<Participation> participations)
        : this(participations, null) { }

    public ParticipationStore(IEventScopedRepository<Participation> participations, INtsSocketContext? socketContext)
    {
        _participations = participations;
        _socketContext = socketContext;
    }

    /// <summary>Without a socket context the store is used as it is: the Event is whichever one the repository serves.</summary>
    bool IsConnected => _socketContext == null || _socketContext.Event != null;

    /// <summary>
    /// Replaced as a whole on every change, never edited in place, so a view that is reading it is not disturbed by
    /// one that arrives meanwhile.
    /// </summary>
    public IReadOnlyList<Participation> Participations { get; private set; } = [];

    protected override async Task<bool> InitializeState()
    {
        if (!IsConnected)
        {
            lock (_lock)
            {
                Participations = [];
            }

            return false;
        }

        int connection;
        lock (_lock)
        {
            connection = _connection;
            _loading = true;
            _refreshedWhileLoading.Clear();
        }

        try
        {
            var loaded = await _participations.ReadMany();
            lock (_lock)
            {
                if (connection != _connection)
                {
                    return false; // the Event was left while the read was out: what it found belongs to a connection that is gone
                }

                Participations = KeepWhatWasRefreshedMeanwhile([.. loaded]);
                return true;
            }
        }
        finally
        {
            lock (_lock)
            {
                _loading = false;
                _refreshedWhileLoading.Clear();
            }
        }
    }

    public Participation? Find(Guid id)
    {
        return Participations.FirstOrDefault(x => x.Id == id);
    }

    public Task Handle(ParticipationChanged notification, CancellationToken cancellationToken)
    {
        if (_socketContext?.Event is { } connected && connected.Id != notification.EventId)
        {
            return Task.CompletedTask; // a message of an Event that is not the connected one
        }

        return Refresh(notification.ParticipationId);
    }

    public async Task Handle(EventConnected notification, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _connection++;
        }

        await ReloadState();
    }

    public Task Handle(EventDisconnected notification, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _connection++;
            Participations = [];
        }

        ClearState();
        return Task.CompletedTask;
    }

    /// <summary>
    /// What the event carries is not used: it is a Participation as it was when the event was made. The Participation
    /// it names is read again. At most one read per Participation is in flight; events that arrive during it ask for
    /// one more read, once, when it has finished, and wait for that one.
    /// </summary>
    Task Refresh(Guid id)
    {
        if (!IsConnected)
        {
            return Task.CompletedTask; // a message that was on its way when the Event was left
        }

        Reading reading;
        lock (_lock)
        {
            if (_readings.TryGetValue(id, out var inFlight))
            {
                inFlight.ReadAgain = true;
                return inFlight.Finished.Task;
            }

            reading = new Reading();
            _readings[id] = reading;
        }

        _ = ReadUntilQuiet(id, reading);
        return reading.Finished.Task;
    }

    async Task ReadUntilQuiet(Guid id, Reading reading)
    {
        try
        {
            while (true)
            {
                int connection;
                lock (_lock)
                {
                    connection = _connection;
                }

                Apply(id, await _participations.Read(id), connection);
                lock (_lock)
                {
                    if (!reading.ReadAgain)
                    {
                        _readings.Remove(id);
                        break;
                    }

                    reading.ReadAgain = false;
                }
            }

            reading.Finished.SetResult();
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _readings.Remove(id);
            }

            reading.Finished.SetException(ex);
        }
    }

    /// <summary>
    /// The load asked before the events that arrived while it was reading, and may answer after them. A Participation
    /// that an event has read again since is newer than the one the load has. Called with the lock held.
    /// </summary>
    List<Participation> KeepWhatWasRefreshedMeanwhile(List<Participation> loaded)
    {
        foreach (var id in _refreshedWhileLoading)
        {
            Put(loaded, id, Find(id));
        }

        return loaded;
    }

    void Apply(Guid id, Participation? persisted, int connection)
    {
        lock (_lock)
        {
            if (connection != _connection)
            {
                return; // read for a connection that is gone
            }

            if (_loading)
            {
                _refreshedWhileLoading.Add(id);
            }

            var items = Participations.ToList();
            if (!Put(items, id, persisted))
            {
                return;
            }

            Participations = items;
        }

        EmitChanged();
    }

    /// <summary>Puts the Participation in the list in place of the one with its id, or takes that one out when there is none.</summary>
    static bool Put(List<Participation> items, Guid id, Participation? participation)
    {
        var index = items.FindIndex(x => x.Id == id);
        if (participation is null)
        {
            if (index < 0)
            {
                return false;
            }

            items.RemoveAt(index);
            return true;
        }

        if (index < 0)
        {
            items.Add(participation);
        }
        else
        {
            items[index] = participation;
        }

        return true;
    }

    sealed class Reading
    {
        public bool ReadAgain { get; set; }
        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
