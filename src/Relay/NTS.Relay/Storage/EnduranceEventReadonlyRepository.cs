using Not.Storage.Repositories;
using Not.Storage.Stores;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.Core;

namespace NTS.Relay.Storage;

public class EnduranceEventReadonlyRepository : ReadonlyRootRepository<EnduranceEvent, CoreState>
{
    public EnduranceEventReadonlyRepository(IStore<CoreState> store)
        : base(store) { }
}
