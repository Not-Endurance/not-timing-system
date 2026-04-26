using Not.Application.Authentication.User;
using Not.Krud.Abstractions;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;


namespace NTS.Application.Contracts.Watcher.Models;

public class NtsUserSessionStateModel
{
    public int? EventId { get; set; }
    public SnapshotGroupModel[] SnapshotHistory { get; set; } = [];

    public IReadOnlyList<SnapshotGroup> GetSnapshotHistory()
    {
        return SnapshotHistory.Select(x => x.MapToDomain()).ToArray();
    }

    public NtsUserSessionStateModel Copy()
    {
        return new NtsUserSessionStateModel
        {
            EventId = EventId,
            SnapshotHistory = SnapshotHistory.Select(group => group.Copy()).ToArray(),
        };
    }
}

