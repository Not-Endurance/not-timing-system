using Not.Domain.Exceptions;
using NTS.Domain.Core.Aggregates;

namespace NTS.Domain.Core.Objects.Startlists;

public record Startlist : ValueObject
{
    static readonly TimeSpan HISTORY_THRESHOLD = TimeSpan.FromMinutes(15);
    static readonly TimeSpan WARNING_THRESHOLD = TimeSpan.FromMinutes(5);

    object _lock = new();
    List<Starter> _history = [];
    List<Starter> _upcoming = [];

    public Startlist(IEnumerable<Participation> participations)
    {
        var upcoming = new List<Starter>();
        var history = new List<Starter>();
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
                var entry = new Starter(
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

    public IReadOnlyList<Starter> History => _history;

    public IReadOnlyList<Starter> Upcoming => _upcoming;

    public IReadOnlyDictionary<int, IReadOnlyList<Starter>> HistoryByStage => GroupByStage(_history);

    public IReadOnlyDictionary<int, IReadOnlyList<Starter>> UpcomingByStage => GroupByStage(_upcoming);

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

    public void UpsertCurrent(Participation participation)
    {
        var current = participation.Phases.Current;
        var currentIndex = participation.Phases.IndexOf(current);
        UpsertStarter(participation, currentIndex, current.StartTime);
    }

    public void UpsertNext(Participation participation)
    {
        var current = participation.Phases.Current;
        var currentIndex = participation.Phases.IndexOf(current);
        if (currentIndex + 1 >= participation.Phases.Count)
        {
            return;
        }

        var nextIndex = currentIndex + 1;
        var start = participation.Phases[nextIndex].StartTime ?? current.GetOutTime();
        UpsertStarter(participation, nextIndex, start);
    }

    public void Remove(int number)
    {
        lock (_lock)
        {
            _upcoming.RemoveAll(x => x.Number == number);
        }
    }

    void UpsertStarter(Participation participation, int phaseIndex, Timestamp? start)
    {
        if (start == null)
        {
            return;
        }

        var phase = participation.Phases[phaseIndex];
        var entry = new Starter(
            participation.Combination.Athlete.Names,
            participation.Combination.Number,
            phaseIndex + 1,
            phase.Length,
            start
        );

        lock (_lock)
        {
            _upcoming.RemoveAll(x => x.Number == entry.Number);
            if (IsHistory(entry))
            {
                return;
            }
            _upcoming = OrderByTimeThenPhase([.. _upcoming, entry]);
        }
    }

    bool IsHistory(Starter entry)
    {
        return entry.Start + HISTORY_THRESHOLD < Timestamp.Now();
    }

    List<Starter> OrderByTimeThenPhase(IEnumerable<Starter> starts)
    {
        return starts.OrderBy(s => s.Start).ThenBy(s => s.PhaseNumber).ToList();
    }

    IReadOnlyDictionary<int, IReadOnlyList<Starter>> GroupByStage(IEnumerable<Starter> starts)
    {
        lock (_lock)
        {
            return starts
                .GroupBy(x => x.PhaseNumber)
                .OrderBy(x => x.Key)
                .ToDictionary(x => x.Key, x => (IReadOnlyList<Starter>)x.ToList().AsReadOnly());
        }
    }
}
