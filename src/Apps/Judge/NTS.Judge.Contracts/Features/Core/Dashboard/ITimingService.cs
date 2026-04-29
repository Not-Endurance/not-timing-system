using NTS.Domain.Aggregates;

namespace NTS.Judge.Contracts.Features.Core.Dashboard;

public interface ISnapshotService
{
    Task Record(Snapshot snapshot);
}
