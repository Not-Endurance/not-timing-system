using NTS.Application.Core;
using NTS.Warp.ACL.Entities;

namespace NTS.Warp.ACL.Factories;

public class ParticipantEntryFactory
{
    public static EmsParticipantEntry Create(CoreParticipationModel participation)
    {
        var emsParticipation = ParticipationFactory.CreateEms(participation);
        return new EmsParticipantEntry(emsParticipation);
    }
}
