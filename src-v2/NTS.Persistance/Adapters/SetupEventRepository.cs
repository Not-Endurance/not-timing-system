using NTS.Domain.Setup.Entities;
using NTS.Persistence.Setup;

namespace NTS.Persistence.Adapters;

public class SetupEventRepository(IStore<SetupState> store) : TreeRepository<Event, SetupState>(store)
{
}