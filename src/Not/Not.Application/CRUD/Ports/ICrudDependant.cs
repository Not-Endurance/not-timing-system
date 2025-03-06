using Not.Domain;
using Not.Injection;

namespace Not.Application.CRUD.Ports;

public interface ICrudDependant<T> : ISingleton
    where T : IAggregateRoot
{
    void Update(T dependable);
}
