using Not.Application.Behinds.Adapters;
using Not.Observables.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;

namespace NTS.Witness.Features.Core.Dashboard;

public interface ISnapshotService : IStatefulService
{
    ObservableList<Participation> Participations { get; }
    IReadOnlyList<Participation> ParticipationsToSnapshot { get; }
    IReadOnlyList<Snapshot> Snapshots { get; }
    IReadOnlyList<SnapshotGroup> History { get; }
    Task<bool> Publish(SnapshotType snapshotType);
    Task RePublish(SnapshotGroup snapshotGroup, SnapshotType snapshotType);
    void CaptureSnapshot(Snapshot snapshot);
    void MoveToSnapshot(Participation participation);
    void RemoveSnapshot(Snapshot snapshot);
    void UpdateSnapshotTimestamp(Snapshot snapshot, Timestamp timestamp);
}
