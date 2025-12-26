using NTS.Warp.ACL.Entities;
using NTS.Warp.Features.Judge.Models;

namespace NTS.Warp.ACL.Factories;

public class ParticipantEntryFactory
{
    public static EmsParticipantEntry Create(ParticipationModel participation)
    {
        var emsParticipation = ParticipationFactory.CreateEms(participation);
        return new EmsParticipantEntry(emsParticipation);
    }
}
