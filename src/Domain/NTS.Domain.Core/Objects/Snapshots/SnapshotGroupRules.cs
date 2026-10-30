using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Domain.Core.Objects.Snapshots;

/// <summary>
/// The rules of a group of Snapshots that was sent (#645, ADR-0013): which answers are only for now, whether the answers are
/// final, and whether a group is sent again as the group it was.
/// </summary>
public static class SnapshotGroupRules
{
    /// <summary>
    /// An answer is only for now when the request did not arrive, or was answered 5xx, 408, 429 or "participation-busy": the same
    /// Snapshots may be sent again. Any other answer (not signed in, not allowed, an Event that has ended) is the server's, and the
    /// same request gets the same one.
    /// </summary>
    public static bool IsOnlyForNow(SnapshotAnswer answer)
    {
        return answer.Status is 0 or 408 or 429 or >= 500
            || string.Equals(answer.ErrorCode, "participation-busy", StringComparison.Ordinal);
    }

    /// <summary>The answers are final when a Snapshot was recorded, or when none of them is only for now.</summary>
    public static bool IsFinal(IEnumerable<SnapshotAnswer> answers)
    {
        var all = answers.ToList();
        return all.Any(x => x.IsRecorded) || !all.Any(IsOnlyForNow);
    }

    /// <summary>
    /// Whether the Snapshots are the ones of the kept group, as the same kind. Then the group is sent again with its id, and the
    /// server answers what it answered before, so that a time is not recorded twice.
    /// </summary>
    public static bool IsSentAgain(SnapshotGroup kept, SnapshotType type, IReadOnlyList<Snapshot> snapshots)
    {
        var before = kept.Entries.ToList();
        return kept.Type == type
            && before.Count == snapshots.Count
            && before.Zip(snapshots, (x, y) => x.Number == y.Number && x.Timestamp == y.Timestamp).All(same => same);
    }
}
