using NTS.Application.Watcher;
using NTS.Domain.Watcher;

namespace NTS.Application.UserSession;

public interface IWitnessUserSession
{
    Task<NtsUserSessionModel?> GetCurrent();
    Task SetEventId(int? eventId);
    Task AppendSnapshot(SnapshotGroup snapshot);
    Task DeleteCurrent();
}
