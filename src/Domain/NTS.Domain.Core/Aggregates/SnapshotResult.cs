using NTS.Domain.Aggregates;

namespace NTS.Domain.Core.Aggregates;

public class SnapshotResult : Aggregate
{
    public static SnapshotResult Applied(int eventId, Snapshot snapshot)
    {
        return new(snapshot, SnapshotResultType.Applied, eventId);
    }

    public static SnapshotResult NotApplied(int eventId, Snapshot snapshot, SnapshotResultType type)
    {
        return new(snapshot, type, eventId);
    }

    public SnapshotResult(Snapshot snapshot, SnapshotResultType type, int eventId, int? id = null)
        : base(id)
    {
        EventId = eventId;
        Snapshot = snapshot;
        Type = type;
    }

    public int EventId { get; }
    public Snapshot Snapshot { get; }
    public SnapshotResultType Type { get; }
}

public enum SnapshotResultType
{
    Applied = 1,
    NotAppliedDueToNotQualified = 2,
    NotAppliedDueToParticipationComplete = 3,
    NotAppliedDueToNotStarted = 4,
    NotAppliedDueToSeparateStageLine = 5,
    NotAppliedDueToSeparateFinishLine = 6,
    NotAppliedDueToDuplicateArrive = 7,
    NotAppliedDueToDuplicateInspect = 8,
    NotAppliedDueToInapplicableAutomatic = 9,
}
