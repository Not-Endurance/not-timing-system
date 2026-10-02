using Not.Application.Authentication.User;
using Not.Krud.Abstractions;
using NTS.Contracts.Shared;
using NTS.Contracts.Shared.Models;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Core.Objects.Snapshots;

namespace NTS.Contracts.Watcher.Models;

public class SnapshotGroupModel
{
    public static SnapshotGroupModel MapFrom(SnapshotGroup group)
    {
        return new SnapshotGroupModel
        {
            Entries = group.Entries.AsEnumerable().Select(SnapshotModel.MapFrom).ToArray(),
            Type = group.Type,
        };
    }

    public SnapshotModel[] Entries { get; set; } = [];
    public SnapshotType Type { get; set; }

    public SnapshotGroup MapToDomain()
    {
        var snapshots = Entries.Select(entry => entry.MapToDomain());
        return new SnapshotGroup(snapshots, Type);
    }

    public SnapshotGroupModel Copy()
    {
        return new SnapshotGroupModel { Entries = Entries.Select(entry => entry.Copy()).ToArray(), Type = Type };
    }
}
