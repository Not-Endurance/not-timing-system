using Not.Application.Behinds.Adapters;
using Not.Domain.Exceptions;
using Not.Injection;
using Not.Notify;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Console;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Objects.Documents;

namespace NoTiming.Ui.Features.Console;

/// <summary>
/// The Handouts of a Live Event in the Console (#646, ADR-0006). A Handout is the id of a Participation and nothing more, and
/// its document is composed from the Participation as the view holds it, so that a correction is in the sheet that is printed
/// next. The server records the times and nobody tells the Console that a Phase completed, so a page that watches reads the
/// change of the store of the Participations and makes the Handout of each one that has a Phase complete that was not before
/// (the last Phase has none: its sheet is the Results). The Handout is made under the id of its Participation, so that two
/// tabs that watch make the one Handout, and a Participation that has one for another reason (one made by hand, or one that
/// came with the Event) is not given a second.
/// </summary>
public class ConsoleHandouts : NStatefulService, IConsoleHandouts, IScoped
{
    readonly IParticipationStore _store;
    readonly ConsoleHandoutWrites _writes;
    readonly INotifier _notifier;
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly List<IViewedEvent> _watching = [];
    Dictionary<Guid, HashSet<Guid>> _completed = [];
    IReadOnlyList<Handout> _handouts = [];
    IViewedEvent? _view;

    public ConsoleHandouts(IParticipationStore store, ConsoleHandoutWrites writes, INotifier notifier)
    {
        _store = store;
        _writes = writes;
        _notifier = notifier;
        Observe(store, MakeForCompletedPhases);
    }

    public IReadOnlyList<ResultsDocument> Documents
    {
        get
        {
            if (_view is not { } view)
            {
                return [];
            }

            var composed = new List<(int Number, ResultsDocument Document)>();
            foreach (var handout in _handouts)
            {
                if (view.Find(handout.ParticipationId) is { } participation)
                {
                    var document = new ResultsDocument(new Result(handout, participation), view.Event, view.Officials);
                    composed.Add((participation.Combination.Number, document));
                }
            }

            return [.. composed.OrderBy(x => x.Number).Select(x => x.Document)];
        }
    }

    public IReadOnlyList<Combination> Combinations =>
        _view == null ? [] : [.. _view.Participations.Select(x => x.Combination).OrderBy(x => x.Number)];

    public async Task Show(IViewedEvent view)
    {
        _view = view;
        await _store.Load();
        await Read(view);
        EmitChanged();
    }

    public IDisposable Watch(IViewedEvent view)
    {
        _completed = _store.Participations.ToDictionary(x => x.Id, CompletedPhasesOf); // what is complete now was handed out before
        _watching.Add(view);
        return new Watching(this, view);
    }

    public async Task Create(int number)
    {
        var view = Shown();
        view.EnsureCanWrite();
        var participation =
            view.Participations.FirstOrDefault(x => x.Combination.Number == number)
            ?? throw new DomainException(Participation_not_found_string);
        await _gate.WaitAsync();
        try
        {
            await Make(view, participation.Id);
            await Read(view);
        }
        finally
        {
            _gate.Release();
        }

        EmitChanged();
    }

    public async Task Delete(IEnumerable<ResultsDocument> documents)
    {
        var view = Shown();
        view.EnsureCanWrite();
        string? refused = null;
        await _gate.WaitAsync();
        try
        {
            foreach (var document in documents)
            {
                await _writes.Delete(document.Id);
                if (_writes.LastError is { } error)
                {
                    refused = error.Message;
                    break;
                }
            }

            await Read(view); // what is shown is what is stored, whether the Api took every one or not
        }
        finally
        {
            _gate.Release();
        }

        EmitChanged();
        if (refused != null)
        {
            throw new DomainException(refused);
        }
    }

    /// <summary>A Participation that has a Phase complete that it did not have before gets its Handout, for the page that watches.</summary>
    async Task MakeForCompletedPhases()
    {
        if (_watching.Count == 0 || _watching[^1] is not { CanWrite: true } view)
        {
            return;
        }

        var completed = new List<Participation>();
        foreach (var participation in _store.Participations)
        {
            var phases = CompletedPhasesOf(participation);
            _completed.TryGetValue(participation.Id, out var before);
            _completed[participation.Id] = phases;
            if (phases.Any(x => before?.Contains(x) != true))
            {
                completed.Add(participation);
            }
        }

        foreach (var participation in completed.OrderBy(x => x.Combination.Number))
        {
            await MakeAfterCompletion(view, participation);
        }
    }

    async Task MakeAfterCompletion(IViewedEvent view, Participation participation)
    {
        await _gate.WaitAsync();
        try
        {
            if (await Make(view, participation.Id))
            {
                _notifier.Inform(string.Format(Handout_made_string, participation.Combination.Number));
            }

            if (_view?.Event.Id == view.Event.Id)
            {
                await Read(_view); // the page that lists them shows it
            }
        }
        catch (DomainException ex)
        {
            _notifier.Warn(string.Format(Handout_not_made_string, participation.Combination.Number, ex.Message));
        }
        finally
        {
            _gate.Release();
        }

        EmitChanged();
    }

    /// <summary>Makes the Handout of a Participation unless it has one; whether it was made. Called with the gate held.</summary>
    async Task<bool> Make(IViewedEvent view, Guid participationId)
    {
        var eventId = view.Event.Id;
        var there = await _writes.ReadMany(x => x.EventId == eventId && x.ParticipationId == participationId);
        Refuse();
        if (there.Any())
        {
            return false;
        }

        await _writes.Create(new Handout(eventId, participationId, participationId));
        Refuse();
        return true;
    }

    /// <summary>Reads the Handouts of the Event, in what the Api answers. Called with the gate held.</summary>
    async Task Read(IViewedEvent view)
    {
        var eventId = view.Event.Id;
        var read = await _writes.ReadMany(x => x.EventId == eventId);
        Refuse();
        _handouts = [.. read];
    }

    void Refuse()
    {
        if (_writes.LastError is { } refused)
        {
            throw new DomainException(refused.Message);
        }
    }

    void Release(IViewedEvent view)
    {
        _watching.Remove(view);
        if (_watching.Count == 0)
        {
            _completed = [];
        }
    }

    IViewedEvent Shown()
    {
        return _view ?? throw new InvalidOperationException("The Handouts of no Event are open.");
    }

    static HashSet<Guid> CompletedPhasesOf(Participation participation)
    {
        return [.. participation.Phases.Where(x => !x.IsFinal && x.IsComplete()).Select(x => x.Id)];
    }

    sealed class Watching : IDisposable
    {
        readonly ConsoleHandouts _handouts;
        readonly IViewedEvent _view;
        bool _released;

        public Watching(ConsoleHandouts handouts, IViewedEvent view)
        {
            _handouts = handouts;
            _view = view;
        }

        public void Dispose()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            _handouts.Release(_view);
        }
    }
}
