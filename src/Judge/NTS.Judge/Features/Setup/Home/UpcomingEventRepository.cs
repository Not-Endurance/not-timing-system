using Not.Storage.Repositories;
using Not.Storage.Stores;
using NTS.Domain.Setup.Aggregates;
using NTS.Storage.Setup;

namespace NTS.Judge.Features.Setup.Home;

public class EnduranceEventHttpRepository : RootRepository<UpcomingEvent, SetupState>
{
    readonly IEventContext _context;

    public EnduranceEventHttpRepository(IStore<SetupState> store, IEventContext context)
        : base(store)
    {
        _context = context;
    }
}
