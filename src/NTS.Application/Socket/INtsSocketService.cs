using NTS.Domain.Core.Aggregates;

namespace NTS.Application.Socket;

public interface INtsSocketService : INtsSocketContext
{
    Task Connect(EnduranceEvent enduranceEvent);
    Task Disconnect();
}
