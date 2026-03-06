using Not.Injection;

namespace Not.Application.Services;

public interface IDeleteBehind<T>
{
    Task Delete(T entity);
}
