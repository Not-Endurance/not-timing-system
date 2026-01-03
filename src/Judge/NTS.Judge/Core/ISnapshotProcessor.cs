using Not.Injection;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Core;

public interface ISnapshotProcessor : ISingleton
{
    Task Process(Snapshot snapshot);
}
