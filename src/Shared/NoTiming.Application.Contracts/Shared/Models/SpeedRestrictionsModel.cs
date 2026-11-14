using NoTiming.Domain.Objects;

namespace NoTiming.Application.Contracts.Shared.Models;

public class SpeedRestrictionsModel
{
    public static SpeedRestrictionsModel From(ISpeedRestrictionAware source)
    {
        return From(source.Restrictions);
    }

    public static SpeedRestrictionsModel From(SpeedRestrictions restrictions)
    {
        return new SpeedRestrictionsModel
        {
            LoopSpeedMin = restrictions.LoopSpeedMin,
            LoopSpeedMax = restrictions.LoopSpeedMax,
            PhaseSpeedMin = restrictions.PhaseSpeedMin,
            PhaseSpeedMax = restrictions.PhaseSpeedMax,
            LoopAverageSpeedMin = restrictions.LoopAverageSpeedMin,
            LoopAverageSpeedMax = restrictions.LoopAverageSpeedMax,
            PhaseAverageSpeedMin = restrictions.PhaseAverageSpeedMin,
            PhaseAverageSpeedMax = restrictions.PhaseAverageSpeedMax,
        };
    }

    public double? LoopSpeedMin { get; set; }
    public double? LoopSpeedMax { get; set; }
    public double? PhaseSpeedMin { get; set; }
    public double? PhaseSpeedMax { get; set; }
    public double? LoopAverageSpeedMin { get; set; }
    public double? LoopAverageSpeedMax { get; set; }
    public double? PhaseAverageSpeedMin { get; set; }
    public double? PhaseAverageSpeedMax { get; set; }

    public SpeedRestrictions ToDomain()
    {
        return new SpeedRestrictions(
            LoopSpeedMin,
            LoopSpeedMax,
            PhaseSpeedMin,
            PhaseSpeedMax,
            LoopAverageSpeedMin,
            LoopAverageSpeedMax,
            PhaseAverageSpeedMin,
            PhaseAverageSpeedMax
        );
    }
}
