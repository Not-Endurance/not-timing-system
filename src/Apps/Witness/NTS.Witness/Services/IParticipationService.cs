using Not.Collections;
using NTS.Domain.Core.Aggregates;

namespace NTS.Witness.Services;

public interface IParticipationService
{
    void Update(Participation participation, NCollectionAction action);
}
