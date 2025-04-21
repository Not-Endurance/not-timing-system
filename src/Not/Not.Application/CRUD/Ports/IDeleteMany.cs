using Not.Domain;

namespace Not.Application.CRUD.Ports;

public interface IDeleteMany<in T>
    where T : IAggregateRoot
{
    Task Delete(params IEnumerable<T> items);
}
