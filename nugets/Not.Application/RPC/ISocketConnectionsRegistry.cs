using Not.Injection;

namespace Not.Application.RPC;

public interface ISocketConnectionsRegistry
{
    void Add(string connectionId);
    void Remove(string connectionId);
}
