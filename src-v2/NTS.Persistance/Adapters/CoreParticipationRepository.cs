using NTS.Domain.Core.Entities;
using NTS.Persistence.States;

namespace NTS.Persistence.Adapters;

public class CoreParticipationRepository(IStore<CoreState> store) : SetRepository<Participation, CoreState>(store)
{
}
