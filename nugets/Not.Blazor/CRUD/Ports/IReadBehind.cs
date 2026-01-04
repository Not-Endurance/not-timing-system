using Not.Domain;
using Not.Injection;

namespace Not.Blazor.CRUD.Ports;

public interface IReadBehind<T> : ISingleton
{
    Task<T?> Read(int id);
}
