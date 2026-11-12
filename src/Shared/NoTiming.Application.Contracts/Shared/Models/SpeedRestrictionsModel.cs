using NoTiming.Domain.Objects;

namespace NoTiming.Application.Contracts.Shared.Models;

public class SpeedRestrictionsModel
{
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

    public double? LoopSpeedMin { get; init; }
    public double? LoopSpeedMax { get; init; }
    public double? PhaseSpeedMin { get; init; }
    public double? PhaseSpeedMax { get; init; }
    public double? LoopAverageSpeedMin { get; init; }
    public double? LoopAverageSpeedMax { get; init; }
    public double? PhaseAverageSpeedMin { get; init; }
    public double? PhaseAverageSpeedMax { get; init; }

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
