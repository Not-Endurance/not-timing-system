using System.Threading.Tasks;

namespace NTS.Witness.RPC.Procedures;

public interface IWitnessSnapshotsHubProcedures
{
    Task ReceiveWitnessEvent(WitnessSnapshotPayload payload);
}
