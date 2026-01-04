using Not.Injection;

namespace Not.Blazor.CRUD.Ports;

public interface IUpdateBehind<T> : ISingleton
{
    Task Update(T model);
}
