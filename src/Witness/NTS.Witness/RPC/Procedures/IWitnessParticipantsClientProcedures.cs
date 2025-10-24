using System.Threading.Tasks;
using NTS.Domain.Core.Aggregates;

namespace NTS.Witness.RPC.Procedures;

public interface IWitnessParticipantsClientProcedures
{
    Task ReceiveEntryUpdate(Participation participation, WitnessCollectionAction action);
}
