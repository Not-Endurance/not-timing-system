using Not.Injection;
using Not.Domain;

namespace Not.Blazor.Ports.Behinds;

public interface IReadBehind<T> : ISingletonService
{
    Task<T?> Read(int id);
}
