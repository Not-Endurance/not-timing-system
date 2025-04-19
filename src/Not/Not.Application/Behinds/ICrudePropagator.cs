using Not.Domain.Base;
using Not.Injection;

namespace Not.Application.Behinds;

public interface ICrudePropagator<in T> : ISingleton
    where T : AggregateRoot
{
    Task Propagate(T aggregate); // TODO can be improved somehow, sure of it
}
