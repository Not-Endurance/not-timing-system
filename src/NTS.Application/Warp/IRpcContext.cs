using Not.Domain.Aggregates;
using Not.Injection;

namespace NTS.Application.Warp;

public interface IRpcContext<T> : ISingleton
    where T : AggregateRoot
{
    T? Root { get; }
    Task Set(T root);
    Task ResetEvent();
}
