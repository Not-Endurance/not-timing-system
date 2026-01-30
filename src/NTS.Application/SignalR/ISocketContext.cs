using Not.Domain;
using Not.Injection;

namespace NTS.Application.SignalR;

public interface ISocketContext<T> : ISingleton
    where T : Aggregate
{
    T? Anchor { get; }
    Task Connect(T anchor);
    Task Disconnect();
}
