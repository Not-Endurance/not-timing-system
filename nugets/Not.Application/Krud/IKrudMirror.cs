using Not.Domain.Aggregates;
using Not.Injection;

namespace Not.Application.Krud;

public interface IKrudMirror<in T> : ISingleton
    where T : IAggregateRoot
{
    Task Reflect(T update);
}
