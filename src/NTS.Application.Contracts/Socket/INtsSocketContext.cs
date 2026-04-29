using System.Diagnostics.CodeAnalysis;
using Not.Application.RPC;
using NTS.Domain.Core.Aggregates;

namespace NTS.Application.Contracts.Socket;

public interface INtsSocketContext : ISocketContext
{
    [MemberNotNullWhen(true, nameof(Event))]
    new bool IsConnected { get; }
    EnduranceEvent? Event { get; }
}
