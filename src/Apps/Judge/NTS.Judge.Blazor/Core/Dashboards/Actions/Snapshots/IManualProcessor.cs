using Not.Injection;
using NTS.Domain.Objects;

namespace NTS.Judge.Blazor.Core.Dashboards.Actions.Snapshots;

public interface IManualProcessor : ISingleton
{
    Task Process(Timestamp timestamp);
}
