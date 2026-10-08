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
    /// The group that was sent and no answer came to, which is sent again as the same group, whatever happens to the page,
    /// until the server answers (#645, ADR-0013); none when nothing waits.
    /// </summary>
    SnapshotGroup? Unanswered { get; }

    /// <summary>
    /// Sends the group that waits again, and again after longer and longer, as long as the person may send it and until the
    /// server answers: the page that shows the Event asks for it when it opens, as the group may have been kept by a page
    /// that was closed.
    /// </summary>
    void Resume(IViewedEvent view);

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
