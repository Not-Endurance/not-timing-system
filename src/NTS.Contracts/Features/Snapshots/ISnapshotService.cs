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

    /// <summary>
    /// Whether the time of an entry of the history can be corrected (#618): it is the latest that the person sent for its start
    /// number as that kind of time, and its group was sent under an id that the history has kept, as the server names the Snapshot
    /// by it (a group that was kept before the ids were has none). The person's access is the view's, as for a Publish.
    /// </summary>
    bool CanUpdate(SnapshotGroup group, Snapshot entry);

    /// <summary>
    /// Corrects the time of an entry of the history at the server (#618, ADR-0013), a write like <see cref="Publish"/> and refused the
    /// same way: the entry shows the new time and the history that is kept has it when the server took it; when it did not, the entry
    /// is left as it is and the person is told why, in the Api's words or as the outcome of the time. The request is absolute, so
    /// one that no answer came to is sent again by the person.
    /// </summary>
    Task UpdateTime(IViewedEvent view, SnapshotGroup group, Snapshot entry, Timestamp time);

    void Capture(Snapshot snapshot);
    void SelectForSnapshot(Participation participation);
    void Remove(Snapshot snapshot);
    void UpdateTimestamp(Snapshot snapshot, Timestamp timestamp);
}
