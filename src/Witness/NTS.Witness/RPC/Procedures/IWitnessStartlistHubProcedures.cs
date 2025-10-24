using System.Collections.Generic;
using System.Threading.Tasks;

namespace NTS.Witness.RPC.Procedures;

public interface IWitnessStartlistHubProcedures
{
    Task<Dictionary<int, WitnessStartlist>> SendStartlist();
}
