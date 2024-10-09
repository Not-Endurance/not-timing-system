using NTS.Domain.Enums;

namespace NTS.Domain.Objects;

public record Snapshot : DomainObject, ISnapshot
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
    public Timestamp Timestamp { get; }

    public static implicit operator Snapshot(RfidSnapshot rfidSnapshot)
    {
        return new (rfidSnapshot.Number, rfidSnapshot.Type, rfidSnapshot.Method, rfidSnapshot.Timestamp);
    }
}
