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

    /// <summary>
    /// The id the device makes for a Snapshot of the group when it sends it (ADR-0013): the id of the time event the server
    /// records it as, and the key of a Snapshot sent again. It is the same every time the group is sent, whatever became of
    /// the first time, so that the server records each Snapshot once, and another group, which is another gesture, makes
    /// other ids, so that the Snapshots sent again as another kind are recorded as that.
    /// </summary>
    public Guid IdOf(Snapshot snapshot)
    {
        var bytes = Id.ToByteArray();
        BitConverter.GetBytes(BitConverter.ToInt32(bytes, 0) + snapshot.Number).CopyTo(bytes, 0);
        return new Guid(bytes);
    }

    IEnumerable<Snapshot> FilterEmptyTimestamps(IEnumerable<Snapshot> snapshots)
    {
        return snapshots.Where(x => x.Timestamp != null).ToList();
    }
}
