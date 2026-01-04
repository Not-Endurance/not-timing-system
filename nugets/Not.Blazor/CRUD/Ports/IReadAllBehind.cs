using Not.Injection;

namespace Not.Blazor.CRUD.Ports;

public interface IReadAllBehind<T> : ISingleton
{
    Task<IEnumerable<T>> GetAll();
}
