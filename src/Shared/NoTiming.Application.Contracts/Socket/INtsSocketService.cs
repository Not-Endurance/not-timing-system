using NC.Application.Behinds.Adapters;
using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Application.Contracts.Socket;

public interface INoTimingSocketService : INoTimingSocketContext, IStatefulService
{
    Task Connect(EventInformation eventInformation);
    Task Disconnect();
    Task<bool> WillResetSession(EventInformation eventInformation);
}
