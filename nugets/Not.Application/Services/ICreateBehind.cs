using Not.Injection;

namespace Not.Application.Services;

// TODO: Remove and use IRepository instead
public interface ICreateBehind<T> : ISingleton
{
    Task Create(T model);
}
