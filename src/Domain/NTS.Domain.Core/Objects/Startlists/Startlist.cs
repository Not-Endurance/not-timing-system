using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;

namespace NTS.Domain.Core.Objects.Startlists;

public record Startlist : ValueObject
{
    static readonly TimeSpan HISTORY_THRESHOLD = TimeSpan.FromMinutes(15);
    static readonly TimeSpan WARNING_THRESHOLD = TimeSpan.FromMinutes(5);

    public Startlist(IEnumerable<Participation> participations)
    {
        var upcoming = new List<Starter>();
        var history = new List<Starter>();
        foreach (var participation in new UniqueParticipations(participations))
        {
            Add(participation, upcoming, history);
        }

        Upcoming = OrderUpcoming(upcoming);
        History = OrderByTimeThenPhase(history);
    }

    public IReadOnlyList<Starter> History { get; }

    public IReadOnlyList<Starter> Upcoming { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<Starter>> HistoryByStage => GroupByStage(History);

    void Add(Participation participation, ICollection<Starter> upcoming, ICollection<Starter> history)
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
            Add(entry, phase.IsComplete(), participation.IsEliminated(), upcoming, history);
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

    Starter CreateStarter(Participation participation, int phaseIndex, Timestamp start)
    {
        var phase = participation.Phases[phaseIndex];
        return new Starter(
            participation.Combination.Athlete.Name,
            participation.Combination.Athlete.NameEnglish,
            participation.Competition.Ruleset,
            participation.Combination.Number,
            phaseIndex + 1,
            phase.Gate,
            phase.Length,
            start
        );
    }

    void Add(
        Starter entry,
        bool forceHistory,
        bool skipUpcoming,
        ICollection<Starter> upcoming,
        ICollection<Starter> history
    )
    {
        if (forceHistory || IsHistory(entry))
        {
            history.Add(entry);
            return;
        }

        if (skipUpcoming)
        {
            return;
        }

        SetState(entry);
        upcoming.Add(entry);
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

    void SetState(Starter entry)
    {
        var now = Timestamp.Now();
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

    IReadOnlyDictionary<int, IReadOnlyList<Starter>> GroupByStage(IEnumerable<Starter> starts)
    {
        return starts
            .GroupBy(x => x.PhaseNumber)
            .OrderBy(x => x.Key)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<Starter>)x.ToList().AsReadOnly());
    }
}
