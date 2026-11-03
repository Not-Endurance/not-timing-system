using NTS.Contracts.Watcher;
using NTS.Contracts.Watcher.Models;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Objects;

namespace NoTiming.Ui.Features.Sessions;

public interface IWitnessUserSession
{
    Task<NtsUserSessionStateModel?> GetCurrent();
    Task SetEventId(Guid? eventId);
    Task AppendSnapshot(SnapshotGroup snapshot);

    /// <summary>
    /// Puts the time that the server holds of a Snapshot, which a correction changed, in the history that is kept (#618): in the
    /// entry of the start number of the group that was sent under the id. A history that has no such entry is left as it is.
    /// </summary>
    Task UpdateSnapshotInHistory(Guid groupId, int number, Timestamp time);

    Task ReplaceSnapshotSelections(IReadOnlyCollection<Snapshot> snapshots);
    Task DeleteCurrent();
}
