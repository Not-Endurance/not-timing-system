using Not.Injection;

namespace Not.Blazor.Ports.Behinds;

public interface IReadAllBehind<T> : ISingletonService
{
    Task<IEnumerable<T>> GetAll();
}
