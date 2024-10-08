using NTS.Domain.Core.Entities;
using NTS.Persistence.States;

namespace NTS.Persistence.Adapters;

public class ClassificationRepository(IStore<CoreState> store) : SetRepository<Ranking, CoreState>(store)
{
}
