using Not.Injection;

namespace Not.Blazor.Ports.Behinds;

public interface IUpdateBehind<T> : ISingletonService
{
    Task<T> Update(T entity);
}
