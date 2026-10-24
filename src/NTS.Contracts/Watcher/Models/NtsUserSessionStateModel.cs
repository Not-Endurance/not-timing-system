using Not.Application.Authentication.User;
using Not.Krud.Abstractions;
using NTS.Contracts.Shared;
using NTS.Contracts.Shared.Models;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Contracts.Watcher.Models;

public class NtsUserSessionStateModel
{
    public SnapshotGroupModel[] SnapshotHistory { get; set; } = [];
    public SnapshotModel[] SnapshotSelections { get; set; } = [];

    public IReadOnlyList<SnapshotGroup> GetSnapshotHistory()
    {
        return SnapshotHistory.Select(x => x.MapToDomain()).ToArray();
    }

    public IReadOnlyList<Snapshot> GetSnapshotSelections()
    {
        return SnapshotSelections.Select(x => x.MapToDomain()).ToArray();
    }

    public NtsUserSessionStateModel Copy()
    {
        return new NtsUserSessionStateModel
        {
            SnapshotHistory = SnapshotHistory.Select(group => group.Copy()).ToArray(),
            SnapshotSelections = SnapshotSelections.Select(snapshot => snapshot.Copy()).ToArray(),
        };
    }
}
