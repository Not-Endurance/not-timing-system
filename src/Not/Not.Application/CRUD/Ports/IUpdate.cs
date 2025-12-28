using Not.Domain.Aggregates;

namespace Not.Application.CRUD.Ports;

public interface IUpdate<in T>
    where T : IAggregateRoot
{
    Task Update(T items);
}
