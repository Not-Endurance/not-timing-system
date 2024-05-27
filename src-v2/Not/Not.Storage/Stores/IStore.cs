using Not.Injection;

namespace Not.Storage.Stores;

public interface IStore<T> : ISingletonService
    where T : class, new()
{
    public Task<T> Load();
    public Task Commit(T state);
}