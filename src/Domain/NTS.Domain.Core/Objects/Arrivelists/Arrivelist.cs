using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;

namespace NTS.Domain.Core.Objects.Arrivelists;

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
        var maxAverageSpeed = ToEstimateSpeed(participation.Combination.MaxAverageSpeed);
        var minAverageSpeed = ToEstimateSpeed(participation.Combination.MinAverageSpeed);
        var averageSpeed = ResolveAverageSpeed(completedDistance, completedInterval);

        // Both models are computed for every Participation regardless of Ruleset - which one an official
        // reads is a presentation choice, so the Arrivelist deliberately consults no Ruleset here.
        var fei = new ArrivalEstimates(
            EstimateArrival(phase.StartTime!, completedDistance, completedInterval, phase.Length, maxAverageSpeed),
            EstimateArrival(phase.StartTime!, completedDistance, completedInterval, phase.Length, averageSpeed),
            EstimateArrival(phase.StartTime!, completedDistance, completedInterval, phase.Length, minAverageSpeed)
        );
        var regional = CreateRegionalEstimates(phase, completedPhases, minAverageSpeed, maxAverageSpeed);

        return new ArrivelistEntry(
            participation.Combination.Number,
            participation.Combination.Athlete.GetDisplayName(participation.Competition.Ruleset),
            participation.Combination.Horse.GetDisplayName(participation.Competition.Ruleset),
            fei,
            regional
        );
    }

    static ArrivalEstimates CreateRegionalEstimates(
        Phase phase,
        IReadOnlyList<Phase> completedPhases,
        double? minAverageSpeed,
        double? maxAverageSpeed
    )
    {
        var lastLoopSpeed = ResolveLastLoopSpeed(completedPhases);
        return new ArrivalEstimates(
            EstimateLoopArrival(phase.StartTime!, phase.Length, maxAverageSpeed),
            EstimateLoopArrival(phase.StartTime!, phase.Length, lastLoopSpeed),
            EstimateLoopArrival(phase.StartTime!, phase.Length, minAverageSpeed)
        );
    }

    static double? ResolveLastLoopSpeed(IReadOnlyList<Phase> completedPhases)
    {
        var lastCompleted = completedPhases.LastOrDefault();
        if (lastCompleted == null || lastCompleted.Length <= 0)
        {
            return null;
        }

        // Timestamp subtraction works on time of day, so a loop ridden across midnight reports a
        // negative interval and blanks the estimate. That is the same arithmetic Total and every
        // displayed loop time already use - deliberately not corrected here, since the Regional
        // Average must agree with the loop speed shown elsewhere.
        var loopInterval = lastCompleted.GetLoopInterval()?.ToTimeSpan();
        if (loopInterval == null || loopInterval <= TimeSpan.Zero)
        {
            return null;
        }

        return lastCompleted.Length / loopInterval.Value.TotalHours;
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

    static double? ResolveAverageSpeed(double completedDistance, TimeSpan completedInterval)
    {
        if (completedDistance <= 0 || completedInterval <= TimeSpan.Zero)
        {
            return null;
        }

        return completedDistance / completedInterval.TotalHours;
    }

    static double? ToEstimateSpeed(Speed? speed)
    {
        var value = speed?.ToDouble();
        return value > 0 ? value : null;
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

    static Timestamp? EstimateLoopArrival(Timestamp start, double phaseDistance, double? speed)
    {
        if (speed == null)
        {
            return null;
        }

        return start.Add(TimeSpan.FromHours(phaseDistance / speed.Value));
    }

    static List<ArrivelistEntry> OrderEntries(IEnumerable<ArrivelistEntry> entries)
    {
        return entries.OrderBy(x => x.SortEstimate == null).ThenBy(x => x.SortEstimate).ThenBy(x => x.Number).ToList();
    }
}
