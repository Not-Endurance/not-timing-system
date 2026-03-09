using Not.Application.Behinds.Adapters;
using Not.Application.RPC;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Socket;

public interface INtsSocketContext : IStatefulService, ISocketContext
{
    UpcomingEvent? Event { get; }
}
