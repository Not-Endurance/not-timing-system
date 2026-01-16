using Not.Injection;

namespace Not.Application.Services;

public interface IDeleteBehind<T> : ISingleton
{
    Task Delete(T entity);
}
