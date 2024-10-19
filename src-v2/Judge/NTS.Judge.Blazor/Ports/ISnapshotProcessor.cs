using Not.Injection;
using NTS.Domain.Objects;

namespace NTS.Judge.Blazor.Ports;

public interface ISnapshotProcessor : ISingletonService
{
    Task Process(Snapshot snapshot);
}
