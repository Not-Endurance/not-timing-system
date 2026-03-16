using NTS.Domain.Setup.Aggregates;

namespace NTS.Application.Socket;

public interface ISocketPrincipalStorage
{
    Task<UpcomingEvent?> Get();
    Task Commit(UpcomingEvent? upcomingEvent);
}
