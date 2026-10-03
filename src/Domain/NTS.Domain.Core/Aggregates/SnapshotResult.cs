using NTS.Domain.Aggregates;

namespace NTS.Domain.Core.Aggregates;

public class SnapshotResult : Aggregate, IEventScoped
{
    public static SnapshotResult Applied(Guid eventId, Snapshot snapshot)
    {
        return new(snapshot, SnapshotResultType.Applied, eventId);
    }

    public static SnapshotResult NotApplied(Guid eventId, Snapshot snapshot, SnapshotResultType type)
    {
        return new(snapshot, type, eventId);
    }

    public SnapshotResult(Snapshot snapshot, SnapshotResultType type, Guid eventId, Guid? id = null)
        : base(id)
    {
        EventId = eventId;
        Snapshot = snapshot;
        Type = type;
    }

    public Guid EventId { get; }
    public Snapshot Snapshot { get; }
    public SnapshotResultType Type { get; }

    internal static SnapshotResult ActivePhaseComplete(Guid eventId, Snapshot snapshot)
    {
        return new(snapshot, SnapshotResultType.ActivePhaseComplete, eventId);
    }
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
    ActivePhaseComplete = 10,
}
