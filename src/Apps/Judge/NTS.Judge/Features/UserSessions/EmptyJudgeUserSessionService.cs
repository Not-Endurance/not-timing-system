using Not.Injection;
using NTS.Application.UserSession;
using NTS.Domain.Core;
using NTS.Domain.Watcher;

namespace NTS.Judge.Features.UserSessions;

internal class EmptyJudgeUserSessionService : IUserSessionService, IScoped
{
    public Task AppendSnapshot(SnapshotGroup snapshot)
    {
        return Task.CompletedTask;
    }

    public Task DeleteCurrent()
    {
        return Task.CompletedTask;
    }

    public Task<ICoreSession?> GetCurrent()
    {
        return Task.FromResult<ICoreSession?>(null);
    }

    public Task SetEventId(int? eventId)
    {
        return Task.CompletedTask;
    }
}
