using Not.Collections;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;

namespace NTS.Witness.Features.Core.State;

public interface IParticipationService : IParticipationContext
{
    void Update(Participation participation, NCollectionAction action);
    void Set(IEnumerable<Participation> participations);
}
