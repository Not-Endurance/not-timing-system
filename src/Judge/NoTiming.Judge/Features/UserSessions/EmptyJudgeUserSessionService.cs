using NC.Injection;
using NoTiming.Application.Contracts.Watcher;
using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Application.UserSession;
using NoTiming.Domain.Watcher;

namespace NoTiming.Judge.Features.UserSessions;

internal class EmptyJudgeUserSessionService : IUiUserSession, IScoped
{
    public Task AppendSnapshot(SnapshotGroup snapshot)
    {
        return Task.CompletedTask;
    }

    public Task ReplaceSnapshotSelections(IReadOnlyCollection<Snapshot> snapshots)
    {
        return Task.CompletedTask;
    }

    public Task DeleteCurrent()
    {
        return Task.CompletedTask;
    }

    public Task<NoTimingUserSessionStateModel?> GetCurrent()
    {
        return Task.FromResult<NoTimingUserSessionStateModel?>(null);
    }

    public Task SetEventId(int? eventId)
    {
        return Task.CompletedTask;
    }
}
