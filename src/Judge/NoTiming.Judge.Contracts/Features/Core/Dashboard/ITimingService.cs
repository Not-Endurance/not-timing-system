using NTS.Domain.Aggregates;

namespace NTS.Judge.Contracts.Features.Core.Dashboard;

public interface IJudgeSnapshotService
{
    Task Record(Snapshot snapshot);
}
