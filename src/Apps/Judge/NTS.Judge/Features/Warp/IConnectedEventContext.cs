using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Warp;

public interface IConnectedEventContext
{
    Task<UpcomingEvent?> Initialize();
    Task Set(UpcomingEvent upcomingEvent);
}
