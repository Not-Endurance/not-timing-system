using Not.Structures;
using NTS.Domain.Enums;

namespace NTS.Domain.Watcher;

public class SnapshotGroup : IIdentifiable
{
    static int _nextId;

    public SnapshotGroup(IEnumerable<Snapshot> snapshots, SnapshotType type)
    {
        Id = Interlocked.Increment(ref _nextId);
        Entries = FilterEmptyTimestamps(snapshots);
        Type = type;
    }

    public int Id { get; }
    public IEnumerable<Snapshot> Entries { get; set; } = [];

    public SnapshotType Type { get; set; }

    IEnumerable<Snapshot> FilterEmptyTimestamps(IEnumerable<Snapshot> snapshots)
    {
        return snapshots.Where(x => x.Timestamp != null).ToList();
    }
}
