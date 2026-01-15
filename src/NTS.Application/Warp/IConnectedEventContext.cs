using NTS.Domain.Setup.Aggregates;

namespace NTS.Application.Warp;

public interface IConnectedEventContext
{
    Task<UpcomingEvent?> Initialize();
    Task Set(UpcomingEvent upcomingEvent);
}
