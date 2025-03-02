using Not.Async;
using Not.Injection;
using Not.Structures;

namespace Not.Application.HTTP;

public abstract class HttpCache<T> : IHttpCache<T>
    where T : class, IIdentifiable
{
    readonly SemaphoreSlim _semaphore = new(1);
    List<T> _cache = [];

    protected abstract Task<IEnumerable<T>> FetchItems();

    public async Task<T?> Get(int id)
    {
        return await List().FirstOrDefault(x => x.Id == id);
    }

    public async Task<IEnumerable<T>> List()
    {
        if (_cache.Count == 0)
        {
            await LoadCache();
        }

        return _cache;
    }

    async Task LoadCache()
    {
        await _semaphore.WaitAsync();
        try
        {
            _cache = await FetchItems().ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

public interface IHttpCache<T> : ISingleton
    where T : class
{
    Task<IEnumerable<T>> List();
    Task<T?> Get(int id);
}
