using NTS.Domain.Objects;

namespace NTS.Application.RPC;

public interface IParticipationClientProcedures
{
    Task ReceiveSnapshots(IEnumerable<Snapshot> snapshots);
}
