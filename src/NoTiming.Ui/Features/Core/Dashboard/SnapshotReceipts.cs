using Not.Domain.Exceptions;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Snapshots;
using NTS.Domain.Core.Objects.Snapshots;

namespace NoTiming.Ui.Features.Core.Dashboard;

/// <summary>What the server answered for the Snapshots of a group, read by the rules of a group (ADR-0013).</summary>
internal static class SnapshotReceipts
{
    public static bool IsRecorded(IReadOnlyList<SnapshotReceipt> receipts, Guid id)
    {
        return receipts.Any(x => x.Id == id && x.IsRecorded);
    }

    public static SnapshotAnswer Answer(SnapshotReceipt receipt)
    {
        return new SnapshotAnswer(receipt.IsRecorded, receipt.Status, receipt.ErrorCode);
    }

    /// <summary>
    /// The person is told why the first Snapshot that was not recorded was not: the Api's own words. The view is told the code,
    /// which says to a view of a Live Event that the Event has ended when it has (#631).
    /// </summary>
    public static void RefuseWhenNotRecorded(IViewedEvent view, IReadOnlyList<SnapshotReceipt> receipts)
    {
        if (receipts.FirstOrDefault(x => !x.IsRecorded) is { } failed)
        {
            view.Refused(failed.ErrorCode);
            throw new DomainException(failed.ErrorMessage ?? failed.ErrorCode ?? "The Snapshot was not recorded.");
        }
    }
}
