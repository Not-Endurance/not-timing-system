using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Contracts.Features.Snapshots;

public interface ISnapshotService : IStatefulService
{
    IReadOnlyList<Participation> Participations { get; }
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
