using Not.Domain;
using Not.Injection;

namespace Not.Application.CRUD.Ports;

public interface IUpdate<T> : ITransient
    where T : IAggregateRoot
{
    Task Update(T entity);
}
