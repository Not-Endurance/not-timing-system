using Not.Domain;

namespace NTS.Application.SignalR;

// TODO: remove this and simply use IRpcSocket.Connect to provide group key. 
public interface IGroupSocketService<T>
    where T : Aggregate
{
    T? Principal { get; }
    Task Connect(T principal);
    Task Disconnect();
}
