using Not.Storage.JsonFile.Repositories;
using Not.Storage.JsonFile.Stores;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class OfficialRepository : SetRepository<Official, CoreState>
{
    public OfficialRepository(IStore<CoreState> store)
        : base(store) { }
}
