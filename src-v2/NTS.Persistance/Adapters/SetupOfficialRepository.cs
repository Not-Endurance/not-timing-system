using NTS.Domain.Setup.Entities;
using NTS.Persistence.Setup;

namespace NTS.Persistence.Adapters;

public class SetupOfficialRepository(IStore<SetupState> store) : SetRepository<Official, SetupState>(store)
{
}
