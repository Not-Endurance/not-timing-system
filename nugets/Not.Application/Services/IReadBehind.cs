using Not.Domain;
using Not.Injection;

namespace Not.Application.Services;

public interface IReadBehind<T> : ISingleton
{
    Task<T?> Read(int id);
}
