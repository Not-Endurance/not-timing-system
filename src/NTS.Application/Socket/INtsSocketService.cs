using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Aggregates;

namespace NTS.Application.Socket;

public interface INtsSocketService : INtsSocketContext, IStatefulService
{
    Task Connect(EnduranceEvent enduranceEvent);
    Task Disconnect();
}
