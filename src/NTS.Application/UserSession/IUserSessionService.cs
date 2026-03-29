using NTS.Application.Watcher;
using NTS.Domain.Watcher;

namespace NTS.Application.UserSession;

public interface IWitnessUserSession
{
    Task<NtsUserSessionStateModel?> GetCurrent();
    Task SetEventId(int? eventId);
    Task AppendSnapshot(SnapshotGroup snapshot);
    Task DeleteCurrent();
}
