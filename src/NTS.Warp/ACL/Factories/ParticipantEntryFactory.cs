using NTS.Domain.Core.Aggregates;
using NTS.Warp.ACL.Entities;
using NTS.Warp.Features.Judge.Models;
using NTS.Warp.Features.Participations;

namespace NTS.Warp.ACL.Factories;

public class ParticipantEntryFactory
{
    public static EmsParticipantEntry Create(ParticipationWarpDto participation)
    {
        var emsParticipation = ParticipationFactory.CreateEms(participation);
        return new EmsParticipantEntry(emsParticipation);
    }
}
