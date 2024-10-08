using Not.Injection;

namespace Not.Blazor.Ports.Behinds;

public interface IDeleteBehind<T> : ISingletonService
{
    Task<T> Delete(T entity);
}
