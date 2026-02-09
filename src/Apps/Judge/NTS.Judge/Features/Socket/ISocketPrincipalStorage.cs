using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Socket;

public interface ISocketPrincipalStorage
{
    Task<UpcomingEvent?> Get();
    Task Commit(UpcomingEvent upcomingEvent);
}
