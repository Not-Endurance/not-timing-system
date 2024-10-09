using NTS.Domain.Enums;

namespace NTS.Domain.Objects;

public record RfidSnapshot : DomainObject, ISnapshot
{
    public RfidSnapshot(int number, SnapshotType type, Timestamp timestamp)
    {
        Number = number;
        Type = type;
        Method = SnapshotMethod.RFID;
        Timestamp = timestamp;
    }
    public int Number { get; }
    public SnapshotType Type { get; }
    public SnapshotMethod Method { get; }
    public Timestamp Timestamp { get; }
}

