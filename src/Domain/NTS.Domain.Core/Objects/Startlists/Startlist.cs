using Not.Domain.Exceptions;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;

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
        foreach (var participation in participations)
        {
            Add(participation);
        }

        _upcoming = OrderByTimeThenPhase(_upcoming);
        _history = OrderByTimeThenPhase(_history);
        UpdateState();
    }

    public IReadOnlyList<Starter> History => _history;

    public IReadOnlyList<Starter> Upcoming => _upcoming;

    public IReadOnlyDictionary<int, IReadOnlyList<Starter>> HistoryByStage => GroupByStage(_history);

    public IReadOnlyDictionary<int, IReadOnlyList<Starter>> UpcomingByStage => GroupByStage(_upcoming);

    public void UpdateState()
    {
        lock (_lock)
        {
            var now = Timestamp.Now();
            foreach (var entry in _upcoming.ToList())
            {
                if (IsHistory(entry))
                {
                    _upcoming.Remove(entry);
                    _history.Add(entry);
                }
            }

            foreach (var entry in _upcoming)
            {
                if (entry.Start < now)
                {
                    entry.State = StartlistEntryState.Late;
                }
                else if (entry.Start - WARNING_THRESHOLD < now)
                {
                    entry.State = StartlistEntryState.Ready;
                }
                else
                {
                    entry.State = StartlistEntryState.Resting;
                }
            }

            _upcoming = OrderUpcoming(_upcoming);
            _history = OrderByTimeThenPhase(_history);
        }
    }

    public void UpsertCurrent(Participation participation)
    {
        var current = participation.Phases.Current;
        var currentIndex = participation.Phases.IndexOf(current);
        UpsertStarter(participation, currentIndex, current.StartTime);
    }

    public void Upsert(Participation participation)
    {
        lock (_lock)
        {
            RemoveAll(participation.Combination.Number);
            Add(participation);
            UpdateState();
        }
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
            RemoveUpcoming(number);
        }
    }

    void Add(Participation participation)
    {
        var phases = participation.Phases;
        for (var phaseIndex = 0; phaseIndex < phases.Count; phaseIndex++)
        {
            var phase = phases[phaseIndex];
            var start = ResolveStart(phases, phaseIndex);
            if (start == null)
            {
                continue;
            }

            var entry = CreateStarter(participation, phaseIndex, start);
            Add(entry, phase.IsComplete());
        }
    }

    Timestamp? ResolveStart(IReadOnlyList<Phase> phases, int phaseIndex)
    {
        var phase = phases[phaseIndex];
        if (phase.StartTime != null)
        {
            return phase.StartTime;
        }

        if (phaseIndex == 0)
        {
            return null;
        }

        var previous = phases[phaseIndex - 1];
        return previous.IsComplete() ? previous.GetOutTime() : null;
    }

    void UpsertStarter(Participation participation, int phaseIndex, Timestamp? start)
    {
        if (start == null)
        {
            return;
        }

        var entry = CreateStarter(participation, phaseIndex, start);

        lock (_lock)
        {
            RemoveUpcoming(entry.Number);
            AddUpcoming(entry);
            UpdateState();
        }
    }

    Starter CreateStarter(Participation participation, int phaseIndex, Timestamp start)
    {
        var phase = participation.Phases[phaseIndex];
        return new Starter(
            participation.Combination.Athlete.Names,
            participation.Combination.Number,
            phaseIndex + 1,
            phase.Length,
            start
        );
    }

    void Add(Starter entry, bool forceHistory = false)
    {
        if (forceHistory || IsHistory(entry))
        {
            _history.Add(entry);
            return;
        }

        _upcoming.Add(entry);
    }

    void AddUpcoming(Starter entry)
    {
        if (IsHistory(entry))
        {
            return;
        }

        _upcoming = [.. _upcoming, entry];
    }

    void RemoveAll(int number)
    {
        RemoveUpcoming(number);
        _history.RemoveAll(x => x.Number == number);
    }

    void RemoveUpcoming(int number)
    {
        _upcoming.RemoveAll(x => x.Number == number);
    }

    bool IsHistory(Starter entry)
    {
        return entry.Start + HISTORY_THRESHOLD < Timestamp.Now();
    }

    List<Starter> OrderByTimeThenPhase(IEnumerable<Starter> starts)
    {
        return starts.OrderBy(s => s.Start).ThenBy(s => s.PhaseNumber).ToList();
    }

    List<Starter> OrderUpcoming(IEnumerable<Starter> starts)
    {
        return starts
            .OrderBy(s => s.State == StartlistEntryState.Late ? 1 : 0)
            .ThenBy(s => s.Start)
            .ThenBy(s => s.PhaseNumber)
            .ToList();
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
