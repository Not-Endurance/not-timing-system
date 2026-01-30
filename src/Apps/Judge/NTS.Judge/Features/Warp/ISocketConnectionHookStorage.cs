using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Warp;

public interface ISocketConnectionHookStorage
{
    Task<UpcomingEvent?> GetConnectionHook();
    Task CommitConnectionHook(UpcomingEvent upcomingEvent);
}
