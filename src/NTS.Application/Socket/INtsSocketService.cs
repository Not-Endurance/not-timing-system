using NTS.Domain.Setup.Aggregates;

namespace NTS.Application.Socket;

public interface INtsSocketService : INtsSocketContext
{
    Task Connect(UpcomingEvent princial);
    Task Disconnect();
}
