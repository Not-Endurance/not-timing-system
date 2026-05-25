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

        return new ArrivelistEntry(
            participation.Combination.Number,
            participation.Combination.Athlete.GetDisplayName(participation.Competition.Ruleset),
            participation.Combination.Horse.GetDisplayName(participation.Competition.Ruleset),
            EstimateArrival(phase.StartTime!, completedDistance, completedInterval, phase.Length, maxAverageSpeed),
            EstimateArrival(phase.StartTime!, completedDistance, completedInterval, phase.Length, averageSpeed),
            EstimateArrival(phase.StartTime!, completedDistance, completedInterval, phase.Length, minAverageSpeed)
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

    static List<ArrivelistEntry> OrderEntries(IEnumerable<ArrivelistEntry> entries)
    {
        return entries
            .OrderBy(x => x.SortEstimate == null)
            .ThenBy(x => x.SortEstimate)
            .ThenBy(x => x.Number)
            .ToList();
    }
}
