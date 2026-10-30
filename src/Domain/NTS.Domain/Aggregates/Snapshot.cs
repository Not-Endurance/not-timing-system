using NTS.Domain.Enums;

namespace NTS.Domain.Aggregates;

public record Snapshot
{
    /// <param name="id">
    /// The id the device made for it (ADR-0013), which is the id of the time event it is recorded as, so that a Snapshot sent
    /// again is told from one that is new. None gives the event an id of its own.
    /// </param>
    public Snapshot(int number, SnapshotType type, SnapshotMethod method, Timestamp timestamp, Guid? id = null)
    {
        Number = number;
        Type = type;
        Method = method;
        Timestamp = timestamp;
        Id = id;
    }

    public Guid? Id { get; }
    public int Number { get; }
    public SnapshotType Type { get; }
    public SnapshotMethod Method { get; }
    public Timestamp Timestamp { get; set; }

    public override string ToString()
    {
        return hash_string + $"{Number} at {Timestamp}";
    }
}
