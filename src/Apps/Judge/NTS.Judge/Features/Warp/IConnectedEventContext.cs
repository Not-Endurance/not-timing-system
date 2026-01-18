using Not.Injection;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Warp;

public interface IConnectedEventContext : ISingleton
{
    Task<UpcomingEvent?> Initialize();
    Task Set(UpcomingEvent upcomingEvent);
}
