using Not.Application.RPC;
using NTS.Domain.Core.Aggregates;

namespace NTS.Application.Socket;

public interface INtsSocketContext : ISocketContext
{
    EnduranceEvent? Event { get; }
}
