using NTS.Domain.Core.Entities;
using NTS.Persistence.States;

namespace NTS.Persistence.Adapters;

public class RankingRepository(IStore<CoreState> store) : SetRepository<Ranking, CoreState>(store)
{
}
