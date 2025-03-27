using NTS.Domain.Core.Aggregates;
using NTS.Relay.ACL.Entities;

namespace NTS.Relay.ACL.Factories;

public class ParticipantEntryFactory
{
    public static EmsParticipantEntry Create(Participation participation)
    {
        var emsParticipation = ParticipationFactory.CreateEms(participation);
        return new EmsParticipantEntry(emsParticipation);
    }
}
