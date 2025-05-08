using Not.Domain;

namespace Not.Application.CRUD.Ports;

internal interface ISafeDelete<T>
    where T : IAggregateRoot
{
    Task SafeDelete(int id);
}
