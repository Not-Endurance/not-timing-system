using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Aggregates;

namespace NTS.Application.Contracts.Socket;

public interface INtsSocketService : INtsSocketContext, IStatefulService
{
    Task Connect(EventInformation eventInformation);
    Task Disconnect();
    Task<bool> WillResetSession(EventInformation eventInformation);
}
