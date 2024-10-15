using NTS.Domain.Setup.Entities;
using NTS.Persistence.Setup;

namespace NTS.Persistence.Adapters;

public class SetupPhaseRepository(IStore<SetupState> store) : SetRepository<Phase, SetupState>(store)
{
}
