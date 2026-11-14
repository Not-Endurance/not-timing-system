using NoTiming.Domain.Core.Aggregates.Participations.Entities;

namespace NoTiming.Domain.Core.Aggregates.Participations.Objects;

// Averages over the phases that are complete, so far in the participation. They are the basis for the average
// speed limits: LoopAverageSpeed excludes recovery, PhaseAverageSpeed includes recovery except on the final phase.
public static class SpeedAverages
{
    public static double? Loop(IEnumerable<Phase> phases)
    {
        var completed = phases.Where(x => x.IsComplete()).ToList();
        var distance = completed.Sum(x => x.Length);
        var time = completed.Aggregate(
            TimeSpan.Zero,
            (sum, phase) => sum + (phase.GetLoopInterval()?.ToTimeSpan() ?? TimeSpan.Zero)
        );
        if (distance <= 0 || time <= TimeSpan.Zero)
        {
            return null;
        }
        return distance / time.TotalHours;
    }

    public static double? Phase(IEnumerable<Phase> phases)
    {
        var completed = phases.Where(x => x.IsComplete()).ToList();
        if (completed.Count == 0)
        {
            return null;
        }
        return new Total(completed).AverageSpeed.ToDouble();
    }
}
