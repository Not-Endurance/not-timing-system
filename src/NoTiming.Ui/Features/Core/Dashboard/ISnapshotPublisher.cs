using NTS.Contracts.Features.Snapshots;
using NTS.Domain.Core.Objects.Snapshots;

namespace NoTiming.Ui.Features.Core.Dashboard;

/// <summary>
/// How the Ui sends the times it captured (#644, ADR-0013): to the Api, which records them and answers for each. Nothing
/// goes over the live connection, and nothing is queued for a Console that is not there.
/// </summary>
public interface ISnapshotPublisher
{
    /// <summary>
    /// Sends the Snapshots of the group to the Event, in order, and says what each came to. A Snapshot that was sent before
    /// is answered with its first outcome and is not recorded again, so the group may be sent again whatever became of the
    /// first time. The answers are in the order of the group.
    /// </summary>
    Task<IReadOnlyList<SnapshotReceipt>> PublishSnapshotsAsync(
        Guid eventId,
        SnapshotGroup snapshotGroup,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Corrects the time of a Snapshot that was sent: an absolute value, so the last one sent wins and one sent again is
    /// harmless. The answer says what changed, or why it was not taken.
    /// </summary>
    Task<SnapshotReceipt> UpdateSnapshotAsync(
        Guid snapshotId,
        DateTimeOffset time,
        CancellationToken cancellationToken = default
    );
}
