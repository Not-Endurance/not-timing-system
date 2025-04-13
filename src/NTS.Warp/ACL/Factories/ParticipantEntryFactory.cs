using NTS.Warp.ACL.Entities;
using NTS.Warp.Features.Judge.Models;

namespace NTS.Warp.ACL.Factories;

public class ParticipantEntryFactory
{
    public static EmsParticipantEntry Create(ParticipationWarpDto participation)
    {
        var emsParticipation = ParticipationFactory.CreateEms(participation);
        return new EmsParticipantEntry(emsParticipation);
    }
}
