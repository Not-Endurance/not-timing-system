using NTS.Domain.Core.Entities;
using NTS.Persistence.States;

namespace NTS.Persistence.Adapters;

public class CoreOfficialRepository(IStore<CoreState> store) : SetRepository<Official, CoreState>(store)
{
}
