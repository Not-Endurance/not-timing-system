using Not.Application.Behinds.Adapters;
using NTS.Contracts.Core;
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

    /// <summary>
    /// Sends the captured Snapshots to the Event the view shows. The service refuses before anything is sent when the view
    /// cannot write (#630, ADR-0007): a domain error, and the Api is not reached.
    /// </summary>
    Task<bool> Publish(IViewedEvent view, SnapshotType snapshotType);

    /// <summary>Sends a Snapshot group again, as a write like <see cref="Publish"/> and refused the same way.</summary>
    Task RePublish(IViewedEvent view, SnapshotGroup snapshotGroup, SnapshotType snapshotType);
    void Capture(Snapshot snapshot);
    void SelectForSnapshot(Participation participation);
    void Remove(Snapshot snapshot);
    void UpdateTimestamp(Snapshot snapshot, Timestamp timestamp);
}
