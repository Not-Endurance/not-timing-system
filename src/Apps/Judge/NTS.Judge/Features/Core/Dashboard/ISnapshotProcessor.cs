using Not.Injection;
using NTS.Domain.Aggregates;

namespace NTS.Judge.Features.Core.Dashboard;

public interface ISnapshotProcessor : ISingleton
{
    Task Process(Snapshot snapshot);
}
