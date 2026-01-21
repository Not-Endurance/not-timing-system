using Not.Collections;
using NTS.Domain.Core.Aggregates;

namespace NTS.Warp.Features.Witness.Procedures;

public interface IParticipantsClientProcedures
{
    Task ReceiveParticipation(Participation participation, NCollectionAction action);
}
