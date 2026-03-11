using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class SnapshotResultRepository : RestApiRepository<SnapshotResult, SnapshotResultModel>, ITransient
{
    public SnapshotResultRepository(NHttpClient client)
        : base("snapshot-results", client) { }
}
