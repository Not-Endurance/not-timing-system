using Not.Application.HTTP;
using NoTiming.Ui.Storage.REST;
using NTS.Contracts.Core.Models;
using NTS.Contracts.Socket;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Storage.Core.Repositories;

public class SnapshotResultEventScopedApiRepository : EventScopedApiRepository<SnapshotResult, SnapshotResultModel>
{
    public SnapshotResultEventScopedApiRepository(
        NHttpClient client,
        EventScopeFactory<SnapshotResult> eventScopeFactory
    )
        : base("snapshot-results", client, eventScopeFactory) { }
}
