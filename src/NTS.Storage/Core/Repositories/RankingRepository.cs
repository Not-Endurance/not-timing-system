using Not.Storage.Repositories;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Temp;

namespace NTS.Storage.Core.Repositories;

public class RankingRepository : SetRepository<Ranking, CoreState>
{
    public RankingRepository(IStore<CoreState> store)
        : base(store) { }
}
