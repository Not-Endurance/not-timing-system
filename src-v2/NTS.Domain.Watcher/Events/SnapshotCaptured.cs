using Not.Domain;
using NTS.Domain.Objects;

namespace NTS.Domain.Watcher.Events;

public abstract record SnapshotCaptured : DomainObject
{
    protected SnapshotCaptured(int number, Snapshot snapshot)
    {
        Number = number;
        Snapshot = snapshot;
    }

    public int Number { get; }
    public Snapshot Snapshot { get; }
}
