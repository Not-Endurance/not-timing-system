using Not.Application.RPC;
using Not.Domain;

namespace NTS.Application.SignalR;

public interface IGroupSocketContext<T>
    where T : Aggregate
{
    T? Hook { get; }
    Task Connect(T hook);
    Task Disconnect();
}
