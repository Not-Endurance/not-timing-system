using Not.Application.CRUD.Ports;
using Not.Async;
using Not.Domain;
using Not.Injection;
using Not.Structures;

namespace Not.Application.HTTP;

public abstract class HttpCache<T> : IHttpCache<T>
    where T : class, IAggregateRoot, IIdentifiable
{
    readonly SemaphoreSlim _semaphore = new(1);
    readonly IRepository<T> _repository;
    List<T> _cache = [];

    protected HttpCache(IRepository<T> repository)
    {
        _repository = repository;
    }

    protected async Task<IEnumerable<T>> FetchItems()
    {
        return await _repository.ReadAll();
    }

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
