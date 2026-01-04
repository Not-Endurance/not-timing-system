using Not.Injection;

namespace Not.Blazor.CRUD.Ports;

public interface ICreateBehind<T> : ISingleton
{
    Task Create(T model);
}
