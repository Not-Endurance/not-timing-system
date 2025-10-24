using System.Collections.Generic;
using NTS.Domain.Enums;
using NTS.Domain.Watcher;

namespace NTS.Witness.RPC;

public class WitnessSnapshotPayload
{
    public IEnumerable<IntermediateSnapshot> Entries { get; init; } = [];

    public SnapshotType Type { get; init; }
}
