using Not.Domain;

namespace Not.Application.CRUD.Ports;

public interface ICreate<in T>
    where T : IAggregateRoot
{
    Task Create(T item);
}
