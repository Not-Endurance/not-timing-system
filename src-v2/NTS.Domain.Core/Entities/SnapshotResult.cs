using Newtonsoft.Json;

namespace NTS.Domain.Core.Entities;

public class SnapshotResult : DomainEntity
{
    public static SnapshotResult Applied(Snapshot snapshot) => new(snapshot, SnapshotResultType.Applied);
    public static SnapshotResult NotApplied(Snapshot snapshot, SnapshotResultType type) => new(snapshot, type);

    [JsonConstructor]
    private SnapshotResult(int id, Snapshot snapshot, SnapshotResultType type) : base(id)
    {
        Snapshot = snapshot;
        Type = type;
    }
    private SnapshotResult(Snapshot snapshot, SnapshotResultType type) : this(GenerateId(), snapshot, type)
    {
    }

    public Snapshot Snapshot { get; private set; }
    public SnapshotResultType Type { get; private set; }
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
