using Not.Application.RPC;
using Not.Domain;

namespace NTS.Application.SignalR;

public interface IGroupSocketContext<T>
    where T : Aggregate
{
    T? Principal { get; }
    Task Connect(T hook);
    Task Disconnect();
}
