using NTS.Domain.Setup.Entities;
using NTS.Persistence.Setup;

namespace NTS.Persistence.Adapters;

public class LoopRepository(IStore<SetupState> store) : SetRepository<Loop, SetupState>(store)
{
}