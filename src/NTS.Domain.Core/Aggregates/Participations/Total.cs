using Not.Domain.Base;

namespace NTS.Domain.Core.Aggregates.Participations;

public record Total : DomainObject
{
    public Total(IEnumerable<Phase> phases)
    {
        if (phases.All(x => !x.IsComplete()))
        {
            throw new GuardException("Do not use Total when all phases are incomplete");
        }
        var completedPhases = phases.Where(x => x.IsComplete()).ToList();
        var totalLength = completedPhases.Sum(x => x.Length);
        RideInterval = completedPhases.Aggregate(
            TimeInterval.Zero,
            (result, x) => result + (x.ArriveTime - x.StartTime) ?? result
        );
        RecoveryInterval = completedPhases.Aggregate(TimeInterval.Zero, (result, x) => (result + x.GetRecoveryInterval())!);
        RecoveryIntervalWithoutFinal =
            RecoveryInterval - completedPhases.FirstOrDefault(x => x.IsFinal)?.GetRecoveryInterval() ?? RecoveryInterval;
        Interval = (RideInterval + RecoveryIntervalWithoutFinal)!;
        AverageSpeed = new Speed(totalLength, Interval);
        LastArriveTime = phases.Last().ArriveTime!;
    }

    public Timestamp LastArriveTime { get; }
    public Speed AverageSpeed { get; }
    public TimeInterval Interval { get; }
    public TimeInterval RideInterval { get; }
    public TimeInterval RecoveryInterval { get; }
    public TimeInterval RecoveryIntervalWithoutFinal { get; }
}
