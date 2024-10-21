using NTS.Domain.Setup.Entities;
using NTS.Persistence.Setup;

namespace NTS.Persistence.Adapters;

public class SetupParticipationRepository(IStore<SetupState> store) : SetRepository<Participation, SetupState>(store)
{
}
