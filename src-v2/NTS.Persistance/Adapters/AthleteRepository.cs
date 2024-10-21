using NTS.Domain.Setup.Entities;
using NTS.Persistence.Setup;

namespace NTS.Persistence.Adapters;

public class AthleteRepository(IStore<SetupState> store) : SetRepository<Athlete, SetupState>(store)
{
}