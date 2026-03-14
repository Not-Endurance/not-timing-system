using NTS.Domain.Enums;
using Not.Structures;

namespace NTS.Domain.Watcher;

public class SnapshotGroup : IIdentifiable
{
    static int _nextId;

    public SnapshotGroup(IEnumerable<Snapshot> snapshots, string type)
    {
        Id = Interlocked.Increment(ref _nextId);
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

    public int Id { get; }
    public IEnumerable<Snapshot> Entries { get; set; } = [];

    public SnapshotType Type { get; set; }
}
