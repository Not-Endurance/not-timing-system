using System.Diagnostics.CodeAnalysis;
using NC.Application.RPC;
using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Application.Contracts.Socket;

public interface INoTimingSocketContext : ISocketContext
{
    [MemberNotNullWhen(true, nameof(Event))]
    new bool IsConnected { get; }
    EventInformation? Event { get; }
}
