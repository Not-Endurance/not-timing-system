using Not.Injection;

namespace Not.Application.Services;

public interface IReadAllBehind<T> : ISingleton
{
    Task<IEnumerable<T>> GetAll();
}
