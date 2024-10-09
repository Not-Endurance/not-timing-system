using NTS.Domain.Objects;

namespace NTS.Judge.Adapters.Behinds;

public interface ISnapshotProcess
{
    Task Process(Snapshot snapshot);
}