using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Witness.RPC.Procedures;

public interface IWitnessHubProcedures
{
    Task<IEnumerable<Participation>> SendParticipants();
    Task<Dictionary<int, Startlist>> SendStartlist();
    Task Receive(WitnessSnapshotPayload payload);
}
