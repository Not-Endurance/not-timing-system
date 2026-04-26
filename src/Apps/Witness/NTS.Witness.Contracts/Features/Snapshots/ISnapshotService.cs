using Not.Application.Behinds.Adapters;
using Not.Observables.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;

namespace NTS.Witness.Contracts.Features.Snapshots;

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
