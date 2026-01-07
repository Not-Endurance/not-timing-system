using Not.Injection;
using NTS.Domain.Objects;

namespace NTS.Judge.Features.Core.Dashboard;

public interface IManualProcessor : ISingleton
{
    Task Process(Timestamp timestamp);
}
