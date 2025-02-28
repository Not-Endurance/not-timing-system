using Not.Application.RPC.SignalR;
using Not.Blazor.Ports;
using Not.Injection;

namespace Not.Application.RPC;

public interface IConnectionsRegistry
{
    void Add(string connectionId);
    void Remove(string connectionId);
}
