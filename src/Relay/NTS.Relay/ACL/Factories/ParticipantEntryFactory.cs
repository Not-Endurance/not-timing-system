using NTS.Relay.ACL.Entities;
using NTS.Domain.Core.Aggregates;

namespace NTS.Relay.ACL.Factories;

public class ParticipantEntryFactory
{
    public static EmsParticipantEntry Create(Participation participation)
    {
        var emsParticipation = ParticipationFactory.CreateEms(participation);
        return new EmsParticipantEntry(emsParticipation);
    }
}
