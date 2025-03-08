using Not.Domain;
using Not.Injection;

namespace Not.Application.CRUD.Ports;

public interface ICrudReflection<T> : ISingleton
    where T : IAggregateRoot
{
    Task Reflect(T dependable);
}
