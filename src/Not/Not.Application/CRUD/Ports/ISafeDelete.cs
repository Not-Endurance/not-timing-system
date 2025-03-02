using Not.Domain;
using Not.Injection;

namespace Not.Application.CRUD.Ports;

internal interface ISafeDelete<T> : ITransient
    where T : IAggregateRoot
{
    Task SafeDelete(int id);
}
