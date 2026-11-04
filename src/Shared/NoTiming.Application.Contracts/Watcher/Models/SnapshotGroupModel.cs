using NC.Application.Authentication.User;
using NC.Krud.Abstractions;
using NoTiming.Application.Contracts.Shared;
using NoTiming.Application.Contracts.Shared.Models;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Objects;
using NoTiming.Domain.Watcher;

namespace NoTiming.Application.Contracts.Watcher.Models;

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
