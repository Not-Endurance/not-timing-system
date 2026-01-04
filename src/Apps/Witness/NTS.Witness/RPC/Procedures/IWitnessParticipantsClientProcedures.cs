using Not.Collections;
using NTS.Domain.Core.Aggregates;

namespace NTS.Witness.RPC.Procedures;

public interface IWitnessParticipantsClientProcedures
{
    Task Receive(Participation participation, NCollectionAction action);
}
