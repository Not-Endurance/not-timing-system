using Not.Injection;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Objects;

namespace NTS.Judge.Core;

public interface ISnapshotProcessor : ISingleton
{
    Task Process(Snapshot snapshot);
}
