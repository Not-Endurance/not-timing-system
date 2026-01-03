using Not.Collections;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Witness.RPC.Procedures;

public interface IWitnessStartlistClientProcedures
{
    Task Receive(StartlistEntry entry, NCollectionAction action);
}
