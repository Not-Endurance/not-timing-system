using NTS.Domain.Core.Entities;
using NTS.Persistence.States;

namespace NTS.Persistence.Adapters;

public class CoreEventRepository(IStore<CoreState> store) : TreeRepository<Event, CoreState>(store)
{
}
