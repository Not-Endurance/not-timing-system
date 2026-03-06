using NTS.Domain.Aggregates;

namespace NTS.Judge.Features.Core.Dashboard;

public interface ITimingService
{
    Task Record(Snapshot snapshot);
}
