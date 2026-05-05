using Not.Application.HTTP;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.REST;

namespace NTS.Storage.Core.Repositories;

public class SnapshotResultEventScopedApiRepository : EventScopedApiRepository<SnapshotResult, SnapshotResultModel>
{
    public SnapshotResultEventScopedApiRepository(NHttpClient client, EventScopeFactory<SnapshotResult> eventScopeFactory)
        : base("snapshot-results", client, eventScopeFactory) { }
}
