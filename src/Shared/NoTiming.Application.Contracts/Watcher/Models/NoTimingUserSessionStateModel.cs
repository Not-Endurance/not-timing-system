using NC.Application.Authentication.User;
using NC.Krud.Abstractions;
using NoTiming.Application.Contracts.Shared;
using NoTiming.Application.Contracts.Shared.Models;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Objects;
using NoTiming.Domain.Watcher;

namespace NoTiming.Application.Contracts.Watcher.Models;

public class NoTimingUserSessionStateModel
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

    public NoTimingUserSessionStateModel Copy()
    {
        return new NoTimingUserSessionStateModel
        {
            SnapshotHistory = SnapshotHistory.Select(group => group.Copy()).ToArray(),
            SnapshotSelections = SnapshotSelections.Select(snapshot => snapshot.Copy()).ToArray(),
        };
    }
}
