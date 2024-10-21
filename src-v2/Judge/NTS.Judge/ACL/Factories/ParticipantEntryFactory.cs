using NTS.Compatibility.EMS.Entities.EMS;
using NTS.Domain.Core.Entities;

namespace NTS.Judge.ACL.Factories;

public class ParticipantEntryFactory
{
    public static EmsParticipantEntry Create(Participation participation)
    {
        var emsParticipation = ParticipationFactory.CreateEms(participation);
        return new EmsParticipantEntry(emsParticipation);
    }
}
