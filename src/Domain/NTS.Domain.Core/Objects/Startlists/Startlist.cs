using NTS.Domain.Core.Aggregates;

namespace NTS.Domain.Core.Objects.Startlists;

// TODO: encapsulate the actual business logic to update entries, rather than having the timer component doing all the work
public class Startlist
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

    public Startlist(IEnumerable<StartlistEntry> entries)
    {
        var upcoming = new List<StartlistEntry>();
        var history = new List<StartlistEntry>();
        foreach(var entry in entries)
        {
            if (IsHistory(entry))
            {
                history.Add(entry);
            }
            else
            {
                upcoming.Add(entry);
            }
            _upcoming = OrderByTimeThenPhase(upcoming);
            _history = OrderByTimeThenPhase(history);
        }
    }

    public IReadOnlyList<StartlistEntry> History => _history;

    public IReadOnlyList<StartlistEntry> Upcoming => _upcoming;

    public void UpdateState()
    {
        lock (_lock)
        {
            var changedHistory = false;
            var now = Timestamp.Now();
            Console.WriteLine(5);
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

    public void Add(StartlistEntry entry)
    {
        lock (_lock)
        {
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
