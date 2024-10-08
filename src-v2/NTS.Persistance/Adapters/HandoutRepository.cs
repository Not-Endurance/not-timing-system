using NTS.Domain.Core.Entities;
using NTS.Persistence.States;

namespace NTS.Persistence.Adapters;

public class HandoutRepository(IStore<CoreState> store) : SetRepository<Handout, CoreState>(store)
{
}
