using Not.Application.RPC;
using Not.Domain;
using Not.Injection;

namespace NTS.Application.SignalR;

public interface IGroupSocketContext<T> : ISocketMetadata, ISingleton
    where T : Aggregate
{
    T? Hook { get; }
    Task Connect(T hook);
    Task Disconnect();
}
