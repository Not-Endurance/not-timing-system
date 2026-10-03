using Not.Structures;
using NTS.Domain.Enums;

namespace NTS.Domain.Core.Objects.Snapshots;

public class SnapshotGroup : IIdentifiable
{
    public SnapshotGroup(IEnumerable<Snapshot> snapshots, SnapshotType type)
    {
        Id = Guid.NewGuid();
        Entries = FilterEmptyTimestamps(snapshots);
        Type = type;
    }

    public Guid Id { get; }
    public IEnumerable<Snapshot> Entries { get; set; } = [];

    public SnapshotType Type { get; set; }

    IEnumerable<Snapshot> FilterEmptyTimestamps(IEnumerable<Snapshot> snapshots)
    {
        return snapshots.Where(x => x.Timestamp != null).ToList();
    }
}
