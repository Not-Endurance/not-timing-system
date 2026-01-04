using Not.Injection;

namespace Not.Application.RPC;

public interface IConnectionsRegistry : ISingleton
{
    void Add(string connectionId);
    void Remove(string connectionId);
}
