using NTS.Application.Warp;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Warp.Dummy;

public class SelectedEventStore : IConnectedEventContext
{
    public Task<UpcomingEvent?> Initialize()
    {
         return Task.FromResult<UpcomingEvent?>(null);
    }

    public Task Set(UpcomingEvent upcomingEvent)
    {
        return Task.CompletedTask;
    }
}
