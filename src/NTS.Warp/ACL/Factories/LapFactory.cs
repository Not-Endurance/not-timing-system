using NTS.Warp.ACL.Entities.Laps;
using NTS.Warp.ACL.Models;
using NTS.Warp.Features.Judge.Models;

namespace NTS.Warp.ACL.Factories;

public class LapFactory
{
    public static IEnumerable<EmsLap> Create(IEnumerable<ParticipationWarpDto.PhaseDto> phases)
    {
        var i = 0;
        foreach (var phase in phases)
        {
            var state = new EmsLapState
            {
                Id = phase.Id,
                IsFinal = phases.Last() == phase,
                IsCompulsoryInspectionRequired = phase.IsRequestedInspectionCompulsory,
                LengthInKm = phase.Length,
                MaxRecoveryTimeInMins = phase.MaxRecovery,
                OrderBy = ++i,
                RestTimeInMins = phase.Rest ?? 0,
            };
            yield return new EmsLap(state);
        }
    }
}
