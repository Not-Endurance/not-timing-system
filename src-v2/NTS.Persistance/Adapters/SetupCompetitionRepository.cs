using Not.Domain;
using NTS.Domain.Setup.Entities;
using NTS.Persistence.Setup;

namespace NTS.Persistence.Adapters;

public class SetupCompetitionRepository(IStore<SetupState> store) : BranchRepository<Competition, SetupState>(store)
{
    protected override Competition? Get(SetupState state, int id)
    {
        return state.Event?.Competitions.FirstOrDefault(x => x.Id == id);
    }

    protected override IParent<Competition>? GetParent(SetupState state, int childId)
    {
        return state.Event;
    }
}
