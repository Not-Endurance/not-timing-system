using Not.Domain.Aggregates;

namespace Not.Application.CRUD.Ports;

public interface ICreate<in T>
{
    Task Create(T item);
}
