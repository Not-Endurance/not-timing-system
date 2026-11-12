namespace NoTiming.Domain.Objects;

// Speed limits in km/h for the four metrics. Each metric has an optional minimum and maximum. A limit can be set on
// the Competition and on the Participation. Both apply, so the stricter value wins.
public sealed record SpeedRestrictions
{
    public static readonly SpeedRestrictions None = new(null, null, null, null, null, null, null, null);

    public static SpeedRestrictions Strictest(SpeedRestrictions? competition, SpeedRestrictions? participation)
    {
        var c = competition ?? None;
        var p = participation ?? None;
        return new SpeedRestrictions(
            Higher(c.LoopSpeedMin, p.LoopSpeedMin),
            Lower(c.LoopSpeedMax, p.LoopSpeedMax),
            Higher(c.PhaseSpeedMin, p.PhaseSpeedMin),
            Lower(c.PhaseSpeedMax, p.PhaseSpeedMax),
            Higher(c.LoopAverageSpeedMin, p.LoopAverageSpeedMin),
            Lower(c.LoopAverageSpeedMax, p.LoopAverageSpeedMax),
            Higher(c.PhaseAverageSpeedMin, p.PhaseAverageSpeedMin),
            Lower(c.PhaseAverageSpeedMax, p.PhaseAverageSpeedMax)
        );
    }

    public SpeedRestrictions(
        double? loopSpeedMin,
        double? loopSpeedMax,
        double? phaseSpeedMin,
        double? phaseSpeedMax,
        double? loopAverageSpeedMin,
        double? loopAverageSpeedMax,
        double? phaseAverageSpeedMin,
        double? phaseAverageSpeedMax
    )
    {
        LoopSpeedMin = loopSpeedMin;
        LoopSpeedMax = loopSpeedMax;
        PhaseSpeedMin = phaseSpeedMin;
        PhaseSpeedMax = phaseSpeedMax;
        LoopAverageSpeedMin = loopAverageSpeedMin;
        LoopAverageSpeedMax = loopAverageSpeedMax;
        PhaseAverageSpeedMin = phaseAverageSpeedMin;
        PhaseAverageSpeedMax = phaseAverageSpeedMax;
    }

    public double? LoopSpeedMin { get; }
    public double? LoopSpeedMax { get; }
    public double? PhaseSpeedMin { get; }
    public double? PhaseSpeedMax { get; }
    public double? LoopAverageSpeedMin { get; }
    public double? LoopAverageSpeedMax { get; }
    public double? PhaseAverageSpeedMin { get; }
    public double? PhaseAverageSpeedMax { get; }

    // A minimum is stricter when it is higher, so the higher of two minimums wins.
    static double? Higher(double? first, double? second)
    {
        return first == null ? second : second == null ? first : Math.Max(first.Value, second.Value);
    }

    // A maximum is stricter when it is lower, so the lower of two maximums wins.
    static double? Lower(double? first, double? second)
    {
        return first == null ? second : second == null ? first : Math.Min(first.Value, second.Value);
    }
}
