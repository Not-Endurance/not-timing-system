using NC.Application.HTTP;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Storage.REST;

namespace NoTiming.Storage.Core.Repositories;

public class SnapshotResultEventScopedApiRepository : EventScopedApiRepository<SnapshotResult, SnapshotResultModel>
{
    public SnapshotResultEventScopedApiRepository(
        NHttpClient client,
        EventScopeFactory<SnapshotResult> eventScopeFactory
    )
        : base("snapshot-results", client, eventScopeFactory) { }
}
