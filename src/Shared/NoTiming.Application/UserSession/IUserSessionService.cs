using NoTiming.Application.Contracts.Watcher;
using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Domain.Watcher;

namespace NoTiming.Application.UserSession;

public interface IUiUserSession
{
    Task<NoTimingUserSessionStateModel?> GetCurrent();
    Task SetEventId(int? eventId);
    Task AppendSnapshot(SnapshotGroup snapshot);
    Task ReplaceSnapshotSelections(IReadOnlyCollection<Snapshot> snapshots);
    Task DeleteCurrent();
}
