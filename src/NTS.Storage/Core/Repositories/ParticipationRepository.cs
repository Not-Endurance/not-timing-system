using Not.Storage.JsonFile.Repositories;
using Not.Storage.JsonFile.Stores;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class ParticipationRepository : SetRepository<Participation, CoreState>
{
    public ParticipationRepository(IStore<CoreState> store)
        : base(store) { }
}
