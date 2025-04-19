using Not.Storage.Repositories;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Storage.Setup.Repositories;

public class EnduranceEventRepository : RootRepository<UpcomingEvent, SetupState>
{
    public EnduranceEventRepository(IStore<SetupState> store)
        : base(store) { }
}
