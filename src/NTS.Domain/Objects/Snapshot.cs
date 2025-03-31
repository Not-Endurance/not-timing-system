using Not.Domain.Base;
using NTS.Domain.Enums;

namespace NTS.Domain.Objects;

public record Snapshot : DomainObject
{
    public Snapshot(int number, SnapshotType type, SnapshotMethod method, Timestamp timestamp)
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
