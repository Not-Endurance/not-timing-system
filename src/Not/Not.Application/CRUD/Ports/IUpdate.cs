using Not.Domain;
using Not.Injection;

namespace Not.Application.CRUD.Ports;

public interface IUpdate<in T> : ITransient
    where T : IAggregateRoot
{
    Task Update(T entity);
}
