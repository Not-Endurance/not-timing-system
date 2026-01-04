using Not.Injection;

namespace Not.Blazor.CRUD.Ports;

public interface IDeleteBehind<T> : ISingleton
{
    Task Delete(T entity);
}
