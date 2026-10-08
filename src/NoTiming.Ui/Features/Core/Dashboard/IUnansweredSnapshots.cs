using NTS.Domain.Core.Objects.Snapshots;

namespace NoTiming.Ui.Features.Core.Dashboard;

/// <summary>
/// The group of Snapshots that this device sent for a person and that no answer came to (#645, ADR-0013). It is kept on the
/// device, before it is sent, so that it is sent again as the same group, with the same ids, after the page was closed,
/// reloaded or lost: that is what keeps a resend of a group whose answer was lost from recording a time twice. It cannot be
/// kept by the host, as it is the one thing the host did not get. It belongs to the person and the Event, so that nobody
/// else who uses the device sends it, and holds nothing of the sign-in: the times, the start numbers, the names the page
/// shows and the id of the group.
/// </summary>
public interface IUnansweredSnapshots
{
    Task<SnapshotGroup?> Read(Guid accountId, Guid eventId);

    /// <summary>Keeps the group, in place of the one that was kept for the person and the Event.</summary>
    Task Keep(Guid accountId, Guid eventId, SnapshotGroup group);

    Task Forget(Guid accountId, Guid eventId);
}
