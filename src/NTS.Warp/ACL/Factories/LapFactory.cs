using Not.Extensions;
using NTS.Warp.ACL.Entities.Laps;
using NTS.Warp.ACL.Models;

namespace NTS.Warp.ACL.Factories;

public class LapFactory
{
    public static IEnumerable<EmsLap> Create(PhaseModel[] phases)
    {
        var i = 0;
        foreach (var phase in phases)
        {
            var state = new EmsLapState
            {
                Id = DomainModelHelper.GenerateId(),
                IsFinal = phases.Last() == phase,
                IsCompulsoryInspectionRequired = CheckCompulsoryTreshold(phase.RecoveryInterval,phase.CompulsoryThresholdInterval,phase.IsFinal),
                LengthInKm = phase.Length,
                MaxRecoveryTimeInMins = phase.MaxRecovery,
                OrderBy = ++i,
                RestTimeInMins = phase.Rest ?? 0,
            };
            yield return new EmsLap(state);
        }
}

    private static bool CheckCompulsoryTreshold(TimeSpan? recoveryInterval, TimeSpan? compulsoryThresholdInterval, bool isFinal)
    {
        if (compulsoryThresholdInterval == null || isFinal)
        {
            return false;
        }
        return recoveryInterval >= compulsoryThresholdInterval;
    }
}
