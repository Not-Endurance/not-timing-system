using Not.Domain.Aggregates;
using Not.Injection;

namespace Not.Application.CRUD.Ports;

public interface IDeleteMany<in T> : ITransient
    where T : IAggregateRoot
{
    Task Delete(params IEnumerable<T> items);
}
