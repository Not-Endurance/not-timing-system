using Not.Application.HTTP;
using Not.Injection;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.REST;

namespace NTS.Storage.Core.Repositories;

public class SnapshotResultRepository : EventScopedApiRepository<SnapshotResult, SnapshotResultModel>, ITransient
{
    public SnapshotResultRepository(NHttpClient client, IServiceProvider serviceProvider)
        : base("snapshot-results", client, serviceProvider) { }
}
