using Not.Storage.JsonFile.Repositories;
using Not.Storage.JsonFile.Stores;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class HandoutRepository : SetRepository<Handout, CoreState>
{
    public HandoutRepository(IStore<CoreState> store)
        : base(store) { }
}
