using NTS.Domain.Watcher;

namespace NTS.Domain.Core;

public interface ICoreSession
{
    int? EventId { get; }
    IReadOnlyList<SnapshotPayload> SnapshotHistory { get; }
}
