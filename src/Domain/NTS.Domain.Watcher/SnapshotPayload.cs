using NTS.Domain.Enums;

namespace NTS.Domain.Watcher;

public class SnapshotPayload
{
    public SnapshotPayload(IEnumerable<IntermediateSnapshot> snapshots, string type)
    {
        Entries = snapshots;
        if (Enum.TryParse(type, out SnapshotType snapshotType))
        {
            Type = snapshotType;
        }
        else
        {
            Type = type == Arrival_string ? SnapshotType.Stage : SnapshotType.Vet;
        }
    }

    public IEnumerable<IntermediateSnapshot> Entries { get; set; } = [];

    public SnapshotType Type { get; set; }
}
