using Not.Domain.Aggregates;
using Not.Injection;

namespace Not.Application.RPC;

public interface IRpcContext<T> : ISingleton
    where T : AggregateRoot
{
    T? Root { get; }
    Task Set(T root);
}
