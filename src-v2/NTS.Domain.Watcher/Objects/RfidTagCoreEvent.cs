using Not.Domain;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Watcher.Entities;

namespace NTS.Domain.Watcher.Objects;

public record RfidTagCoreEvent : DomainObject

{
    public RfidTagCoreEvent(RfidTag tag, SnapshotType type, SnapshotMethod method)
    {
        Number = int.Parse(tag.Number); // TODO: better parsing
    }
    public int Number{ get; }
    public ISnapshot Snapshot { get; } = default!;
}
