using Not.Injection;

namespace Not.Blazor.Ports.Behinds;

public interface ICreateBehind<T> : ISingletonService
{
    Task<T> Create(T model);
}
