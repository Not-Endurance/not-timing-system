using Not.Domain;
using NTS.Domain.Setup.Entities;
using NTS.Persistence.Setup;

namespace NTS.Persistence.Adapters;

public class SetupCompetitionRepository(IStore<SetupState> store) : SetRepository<Competition, SetupState>(store)
{
}
