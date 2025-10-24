using System.Threading.Tasks;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Witness.RPC.Procedures;

public interface IWitnessStartlistClientProcedures
{
    Task ReceiveEntry(StartlistEntry entry, WitnessCollectionAction action);
}
