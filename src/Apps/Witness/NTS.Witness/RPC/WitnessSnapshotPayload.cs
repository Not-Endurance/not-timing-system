using NTS.Domain.Enums;
using NTS.Domain.Watcher;

namespace NTS.Witness.RPC;

public class WitnessSnapshotPayload // TODO: should be SnapshotModel
{
    public IEnumerable<IntermediateSnapshot> Entries { get; init; } = [];

    public SnapshotType Type { get; init; }
}
