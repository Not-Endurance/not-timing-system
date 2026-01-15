using Not.Storage.JsonFile.Repositories;
using Not.Storage.JsonFile.Stores;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class RankingRepository : SetRepository<Ranking, CoreState>
{
    public RankingRepository(IStore<CoreState> store)
        : base(store) { }
}
