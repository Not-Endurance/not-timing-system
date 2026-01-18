using Newtonsoft.Json;
using Not.Domain.Aggregates;
using NTS.Domain.Aggregates;

namespace NTS.Domain.Core.Aggregates;

public class SnapshotResult : Aggregate
{
    public static SnapshotResult Applied(Snapshot snapshot)
    {
        return new(snapshot, SnapshotResultType.Applied);
    }

    public static SnapshotResult NotApplied(Snapshot snapshot, SnapshotResultType type)
    {
        return new(snapshot, type);
    }

    [JsonConstructor]
    SnapshotResult(int id, Snapshot snapshot, SnapshotResultType type)
        : base(id)
    {
        Snapshot = snapshot;
        Type = type;
    }

    SnapshotResult(Snapshot snapshot, SnapshotResultType type)
        : this(GenerateId(), snapshot, type) { }

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
