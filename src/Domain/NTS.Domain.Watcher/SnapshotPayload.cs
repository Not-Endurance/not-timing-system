using NTS.Domain.Enums;

namespace NTS.Domain.Watcher;

public class SnapshotPayload
{
    public SnapshotPayload(IEnumerable<IntermediateSnapshot> snapshots, string type)
    {
        Entries = snapshots;
        Type = type == SnapshotType.Stage.ToString() ? SnapshotType.Stage : SnapshotType.Vet;
    }
    public IEnumerable<IntermediateSnapshot> Entries { get; set; } = [];

    public SnapshotType Type { get; set; }
}
