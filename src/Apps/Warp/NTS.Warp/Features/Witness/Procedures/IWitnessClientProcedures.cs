using Not.Collections;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Warp.Features.Witness.Procedures;

public interface IWitnessClientProcedures
{
    Task ReceiveParticipation(Participation participation, NCollectionAction action);
    Task ReceiveStartlistEntry(StartlistEntry entry, NCollectionAction action);
}
