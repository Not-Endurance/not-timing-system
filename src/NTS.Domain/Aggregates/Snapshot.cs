using Not.Domain.Aggregates;
using NTS.Domain.Enums;

namespace NTS.Domain.Aggregates;

public class Snapshot : AggregateRoot
{
    public Snapshot(int number, SnapshotType type, SnapshotMethod method, Timestamp timestamp)
        : base(GenerateId())
    {
        Number = number;
        Type = type;
        Method = method;
        Timestamp = timestamp;
    }

    public int Number { get; }
    public SnapshotType Type { get; }
    public SnapshotMethod Method { get; }
    public Timestamp Timestamp { get; set; }

    public override string ToString()
    {
        return hash_string + $"{Number} at {Timestamp}";
    }
}
