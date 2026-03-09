using NTS.Domain.Setup.Aggregates;

namespace NTS.Application.Socket;

public interface INtsSocketContext
{
    UpcomingEvent? Event { get; }
}
