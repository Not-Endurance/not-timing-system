using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Aggregates.Participations.Entities;
using NoTiming.Domain.Core.Aggregates.Participations.Objects;
using NoTiming.Domain.Objects;

namespace NoTiming.Domain.Core.Objects.Arrivelists;

public record Arrivelist : ValueObject
{
    List<ArrivelistEntry> _entries = [];

    public Arrivelist(IEnumerable<Participation> participations)
    {
        _entries = OrderEntries(new UniqueParticipations(participations).Select(CreateEntry).OfType<ArrivelistEntry>());
    }

    public IReadOnlyList<ArrivelistEntry> Entries => _entries;

    static ArrivelistEntry? CreateEntry(Participation participation)
    {
        if (participation.IsEliminated() || participation.IsComplete())
        {
            return null;
        }

        var activePhase = FindActivePhase(participation);
        if (activePhase == null)
        {
            return null;
        }

        var (phase, phaseIndex) = activePhase.Value;
        var completedPhases = participation.Phases.Take(phaseIndex).Where(x => x.IsComplete()).ToList();
        var completedDistance = completedPhases.Sum(x => x.Length);
        var completedInterval = ResolveCompletedInterval(completedPhases);
        var completedLoopInterval = ResolveCompletedLoopInterval(completedPhases);
        var averageSpeed = ResolveAverageSpeed(completedDistance, completedInterval);
        var window = ResolveWindow(
            phase,
            completedDistance,
            completedLoopInterval,
            completedInterval,
            participation.Combination.Restrictions
        );

        return new ArrivelistEntry(
            participation.Combination.Number,
            participation.Combination.Athlete.GetDisplayName(participation.Competition.EnglishNames),
            participation.Combination.Horse.GetDisplayName(participation.Competition.EnglishNames),
            window.Fast,
            EstimateArrival(phase.StartTime!, completedDistance, completedInterval, phase.Length, averageSpeed),
            window.Slow
        );
    }

    static (Phase Phase, int Index)? FindActivePhase(Participation participation)
    {
        for (var index = 0; index < participation.Phases.Count; index++)
        {
            var phase = participation.Phases[index];
            if (phase.StartTime == null || phase.ArriveTime != null)
            {
                continue;
            }

            if (phase.StartTime.ToDateTimeOffset() > DateTimeOffset.Now)
            {
                return null;
            }

            return (phase, index);
        }

        return null;
    }

    static TimeSpan ResolveCompletedInterval(IReadOnlyCollection<Phase> completedPhases)
    {
        return completedPhases.Count == 0 ? TimeSpan.Zero : new Total(completedPhases).Interval.ToTimeSpan();
    }

    static TimeSpan ResolveCompletedLoopInterval(IEnumerable<Phase> completedPhases)
    {
        return completedPhases.Aggregate(
            TimeSpan.Zero,
            (sum, phase) => sum + (phase.GetLoopInterval()?.ToTimeSpan() ?? TimeSpan.Zero)
        );
    }

    static double? ResolveAverageSpeed(double completedDistance, TimeSpan completedInterval)
    {
        if (completedDistance <= 0 || completedInterval <= TimeSpan.Zero)
        {
            return null;
        }

        return completedDistance / completedInterval.TotalHours;
    }

    // The arrival window at the end of the current phase. Fast is the latest of the earliest bounds set by the
    // maximum limits. Slow is the earliest of the latest bounds set by the minimum limits. Phase-based limits
    // include recovery, which isn't known yet: it lies between zero and the phase's maximum recovery. Fast takes
    // the maximum recovery and Slow takes zero, so the window covers every possible recovery.
    static (Timestamp? Fast, Timestamp? Slow) ResolveWindow(
        Phase phase,
        double completedDistance,
        TimeSpan completedLoopInterval,
        TimeSpan completedPhaseInterval,
        SpeedRestrictions restrictions
    )
    {
        var start = phase.StartTime!;
        var totalDistance = completedDistance + phase.Length;
        var fastRecovery = phase.IsFinal ? TimeSpan.Zero : TimeSpan.FromMinutes(phase.MaxRecovery);

        var earliest = new List<TimeSpan>();
        var latest = new List<TimeSpan>();

        if (Limit(restrictions.LoopSpeedMax) is { } loopMax)
        {
            earliest.Add(TimeSpanFromSpeed(phase.Length, loopMax));
        }
        if (Limit(restrictions.LoopSpeedMin) is { } loopMin)
        {
            latest.Add(TimeSpanFromSpeed(phase.Length, loopMin));
        }
        if (Limit(restrictions.PhaseSpeedMax) is { } phaseMax)
        {
            earliest.Add(TimeSpanFromSpeed(phase.Length, phaseMax) - fastRecovery);
        }
        if (Limit(restrictions.PhaseSpeedMin) is { } phaseMin)
        {
            latest.Add(TimeSpanFromSpeed(phase.Length, phaseMin));
        }
        if (Limit(restrictions.LoopAverageSpeedMax) is { } loopAverageMax)
        {
            earliest.Add(TimeSpanFromSpeed(totalDistance, loopAverageMax) - completedLoopInterval);
        }
        if (Limit(restrictions.LoopAverageSpeedMin) is { } loopAverageMin)
        {
            latest.Add(TimeSpanFromSpeed(totalDistance, loopAverageMin) - completedLoopInterval);
        }
        if (Limit(restrictions.PhaseAverageSpeedMax) is { } phaseAverageMax)
        {
            earliest.Add(TimeSpanFromSpeed(totalDistance, phaseAverageMax) - completedPhaseInterval - fastRecovery);
        }
        if (Limit(restrictions.PhaseAverageSpeedMin) is { } phaseAverageMin)
        {
            latest.Add(TimeSpanFromSpeed(totalDistance, phaseAverageMin) - completedPhaseInterval);
        }

        var fast = earliest.Count == 0 ? (Timestamp?)null : start.Add(earliest.Max());
        var slow = latest.Count == 0 ? (Timestamp?)null : start.Add(latest.Min());
        return (fast, slow);
    }

    // A limit of zero or less is not a limit. Dividing by it would never arrive.
    static double? Limit(double? speed)
    {
        return speed > 0 ? speed : null;
    }

    static TimeSpan TimeSpanFromSpeed(double distance, double speed)
    {
        return TimeSpan.FromHours(distance / speed);
    }

    static Timestamp? EstimateArrival(
        Timestamp start,
        double completedDistance,
        TimeSpan completedInterval,
        double phaseDistance,
        double? speed
    )
    {
        if (speed == null)
        {
            return null;
        }

        var totalDistanceAtArrival = completedDistance + phaseDistance;
        var requiredTotalInterval = TimeSpan.FromHours(totalDistanceAtArrival / speed.Value);
        var remainingInterval = requiredTotalInterval - completedInterval;
        return start.Add(remainingInterval);
    }

    static List<ArrivelistEntry> OrderEntries(IEnumerable<ArrivelistEntry> entries)
    {
        return entries.OrderBy(x => x.SortEstimate == null).ThenBy(x => x.SortEstimate).ThenBy(x => x.Number).ToList();
    }
}
