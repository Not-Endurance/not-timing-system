using Not.Injection;
using NTS.Application.Contracts.Watcher;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Application.UserSession;
using NTS.Domain.Watcher;

namespace NTS.Judge.Features.UserSessions;

internal class EmptyJudgeUserSessionService : IWitnessUserSession, IScoped
{
    public Task AppendSnapshot(SnapshotGroup snapshot)
    {
        return Task.CompletedTask;
    }

    public Task DeleteCurrent()
    {
        return Task.CompletedTask;
    }

    public Task<NtsUserSessionStateModel?> GetCurrent()
    {
        return Task.FromResult<NtsUserSessionStateModel?>(null);
    }

    public Task SetEventId(int? eventId)
    {
        return Task.CompletedTask;
    }
}
