using NTS.Domain.Core;
using NTS.Domain.Watcher;

namespace NTS.Application.UserSession;

public interface IUserSessionService
{
    Task<ICoreSession?> GetCurrent();
    Task SetEventId(int? eventId);
    Task AppendSnapshot(SnapshotGroup snapshot);
    Task DeleteCurrent();
}
