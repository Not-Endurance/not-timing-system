using Not.Storage.JsonFile.Repositories;
using Not.Storage.JsonFile.Stores;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class SnapshotResultRepository : SetRepository<SnapshotResult, CoreState>
{
    public SnapshotResultRepository(IStore<CoreState> store)
        : base(store) { }
}
