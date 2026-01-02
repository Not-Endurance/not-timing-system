using NTS.Application.Models;
using NTS.Warp.ACL.Entities.Laps;
using NTS.Warp.ACL.Models;

namespace NTS.Warp.ACL.Factories;

public class LapFactory
{
    public static IEnumerable<EmsLap> Create(CorePhaseModel[] phases)
    {
        if (phases == null || phases.Length == 0)
        {
            yield break;
        }
        var i = 0;
        foreach (var phase in phases)
        {
            var state = new EmsLapState
            {
                Id = phase.Id,
                IsFinal = phases.Last() == phase,
                IsCompulsoryInspectionRequired = (bool)phase.IsRequiredInspectionCompulsory!,
                LengthInKm = (double)phase.Length!,
                MaxRecoveryTimeInMins = (int)phase.MaxRecovery!,
                OrderBy = ++i,
                RestTimeInMins = phase.Rest ?? 0,
            };
            yield return new EmsLap(state);
        }
    }
}
