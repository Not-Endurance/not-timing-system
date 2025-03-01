using Not.Domain;
using Not.Injection;

namespace Not.Application.CRUD.Ports;

public interface ICreate<T> : ITransient
    where T : IAggregateRoot
{
    Task Create(T entity);
}
