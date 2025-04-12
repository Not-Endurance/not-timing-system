using Not.Storage.Repositories;
using Not.Storage.Stores;
using NTS.Domain.Core.Aggregates;
using NTS.Storage.Core;

namespace NTS.Relay.Storage;

internal class ParticipationReadonlyRepository : ReadonlySetRepository<Participation, CoreState>
{
    public ParticipationReadonlyRepository(IStore<CoreState> store)
        : base(store) { }
}
