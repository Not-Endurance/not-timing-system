using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Witness.RPC.Procedures;

public interface IWitnessStartlistHubProcedures
{
    Task<Dictionary<int, Startlist>> SendStartlist();
}
