using Not.Domain.Exceptions;
using NTS.Domain.Core.Aggregates;

namespace NTS.Domain.Core.Objects.Startlists;

public record Startlist
{
    static readonly TimeSpan HISTORY_THRESHOLD = TimeSpan.FromMinutes(15);
    static readonly TimeSpan WARNING_THRESHOLD = TimeSpan.FromMinutes(5);

    object _lock = new();
    List<StartlistEntry> _history = [];
    List<StartlistEntry> _upcoming = [];

    public Startlist(IEnumerable<Participation> participations)
    {
        var upcoming = new List<StartlistEntry>();
        var history = new List<StartlistEntry>();
        foreach (var participation in participations)
        {
            if (participation.IsEliminated())
            {
                continue;
            }
            var phases = participation.Phases;
            foreach (var phase in phases)
            {
                if (phase.StartTime == null)
                {
                    continue;
                }
                var phaseIndex = phases.IndexOf(phase);
                var phaseNumber = phaseIndex + 1;
                var entry = new StartlistEntry(
                    participation.Combination.Athlete.Names,
                    participation.Combination.Number,
                    phaseNumber,
                    phases[phaseIndex].Length,
                    phase.StartTime.ToDateTimeOffset()!
                );
                if (IsHistory(entry))
                {
                    history.Add(entry);
                }
                else
                {
                    upcoming.Add(entry);
                }
            }
        }

        _upcoming = OrderByTimeThenPhase(upcoming);
        _history = OrderByTimeThenPhase(history);
    }

    public IReadOnlyList<StartlistEntry> History => _history;

    public IReadOnlyList<StartlistEntry> Upcoming => _upcoming;

    public void UpdateState()
    {
        lock (_lock)
        {
            var changedHistory = false;
            var now = Timestamp.Now();
            foreach (var entry in _upcoming.ToList())
            {
                if (IsHistory(entry))
                {
                    _upcoming.Remove(entry);
                    _history.Add(entry);
                    changedHistory = true;
                }
                else if (entry.Start < now)
                {
                    entry.State = StartlistEntryState.Late;
                }
                else if (entry.Start - WARNING_THRESHOLD < now)
                {
                    entry.State = StartlistEntryState.Ready;
                }
            }

            if (changedHistory)
            {
                _history = OrderByTimeThenPhase(_history);
            }
        }
    }

    public void Add(Participation participation)
    {
        var index = participation.Phases.IndexOf(participation.Phases.Current);
        if (participation.Phases.Count <= ++index)
        {
            throw new DomainException(Cannot_add_completed_participations_in_startlist);
        }
        var phase =
            participation.Phases[index].StartTime != null ? participation.Phases[index] : participation.Phases.Current;
        var phaseNumber = participation.Phases.NumberOf(phase);
        var start = new Timestamp(phase.StartTime!.ToDateTimeOffset());
        var entry = new StartlistEntry(
            participation.Combination.Athlete.Names,
            participation.Combination.Number,
            phaseNumber,
            phase.Length,
            start
        );

        Add(entry);
    }

    public void Add(StartlistEntry entry)
    {
        lock (_lock)
        {
            if (IsHistory(entry))
            {
                return;
            }
            _upcoming = OrderByTimeThenPhase([.. _upcoming, entry]);
        }
    }

    public void Remove(int number)
    {
        lock (_lock)
        {
            var match = _upcoming.FirstOrDefault(x => x.Number == number);
            if (match != null)
            {
                _upcoming.Remove(match);
            }
        }
    }

    bool IsHistory(StartlistEntry entry)
    {
        return entry.Start + HISTORY_THRESHOLD < Timestamp.Now();
    }

    List<StartlistEntry> OrderByTimeThenPhase(IEnumerable<StartlistEntry> starts)
    {
        return starts.OrderBy(s => s.Start).ThenBy(s => s.PhaseNumber).ToList();
    }
}
