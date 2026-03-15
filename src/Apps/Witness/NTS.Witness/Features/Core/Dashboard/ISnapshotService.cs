using Not.Observables.Structures;
using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;

namespace NTS.Witness.Features.Core.Dashboard;

public interface ISnapshotService : IStatefulService
{
    ObservableList<Participation> Participations { get; }
    IReadOnlyList<Participation> ParticipationsToSnapshot { get; }
    IReadOnlyList<Snapshot> Snapshots { get; }
    IReadOnlyList<SnapshotGroup> History { get; }
    Task<bool> Publish(string snapshotType);
    Task RePublish(SnapshotGroup snapshotGroup);
    void CaptureSnapshot(Snapshot snapshot);
    void MoveToSnapshot(Participation participation);
    void UpdateSnapshotTimestamp(Snapshot snapshot, Timestamp timestamp);
}
