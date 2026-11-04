using NoTiming.Domain.Aggregates;

namespace NoTiming.Judge.Contracts.Features.Core.Dashboard;

public interface IJudgeSnapshotService
{
    Task Record(Snapshot snapshot);
}
