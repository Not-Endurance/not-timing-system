using NC.Application.Behinds.Adapters;
using NC.Observables.Structures;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Objects;
using NoTiming.Domain.Watcher;

namespace NoTiming.Ui.Contracts.Features.Snapshots;

public interface ISnapshotService : IStatefulService
{
    ObservableList<Participation> Participations { get; }
    IReadOnlyList<Participation> ParticipationsToSnapshot { get; }
    IReadOnlyList<Snapshot> Snapshots { get; }
    IReadOnlyList<SnapshotGroup> History { get; }
    Task<bool> Publish(SnapshotType snapshotType);
    Task RePublish(SnapshotGroup snapshotGroup, SnapshotType snapshotType);
    void Capture(Snapshot snapshot);
    void SelectForSnapshot(Participation participation);
    void Remove(Snapshot snapshot);
    void UpdateTimestamp(Snapshot snapshot, Timestamp timestamp);
}
