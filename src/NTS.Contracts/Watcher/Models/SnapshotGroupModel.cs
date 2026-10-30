using Not.Application.Authentication.User;
using Not.Krud.Abstractions;
using NTS.Contracts.Shared;
using NTS.Contracts.Shared.Models;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Contracts.Watcher.Models;

public class SnapshotGroupModel
{
    public static SnapshotGroupModel MapFrom(SnapshotGroup group)
    {
        return new SnapshotGroupModel
        {
            Entries = group.Entries.AsEnumerable().Select(SnapshotModel.MapFrom).ToArray(),
            Type = group.Type,
            GroupId = group.Id,
        };
    }

    public SnapshotModel[] Entries { get; set; } = [];
    public SnapshotType Type { get; set; }

    /// <summary>
    /// The id the group was sent under (ADR-0013), which with a start number is the id of the Snapshot at the server: what a
    /// correction of the time of an entry names (#618). None for a group that was kept before the ids were.
    /// </summary>
    public Guid? GroupId { get; set; }

    public SnapshotGroup MapToDomain()
    {
        var snapshots = Entries.Select(entry => entry.MapToDomain());
        return new SnapshotGroup(snapshots, Type, GroupId);
    }

    public SnapshotGroupModel Copy()
    {
        return new SnapshotGroupModel
        {
            Entries = Entries.Select(entry => entry.Copy()).ToArray(),
            Type = Type,
            GroupId = GroupId,
        };
    }
}
