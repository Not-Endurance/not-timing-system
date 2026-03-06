using NTS.Domain.Setup.Aggregates;

namespace NTS.Application.Socket;

public interface INtsSocketService
{
    UpcomingEvent? Event { get; }
    Task Connect(UpcomingEvent princial);
    Task Disconnect();
}
