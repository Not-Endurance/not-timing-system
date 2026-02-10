using Not.Application.RPC;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Socket;

public interface INtsSocketContext : ISocketContext
{
    UpcomingEvent? Event { get; }
}
