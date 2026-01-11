using Not.Injection;

namespace Not.Application.Services;

public interface IUpdateBehind<T> : ISingleton
{
    Task Update(T model);
}
