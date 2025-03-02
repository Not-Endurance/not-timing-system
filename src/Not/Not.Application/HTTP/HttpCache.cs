using Not.Application.CRUD.Ports;
using Not.Async;
using Not.Domain;
using Not.Cache;
using Not.Structures;

namespace Not.Application.HTTP;

public abstract class HttpCache<T> : ICache<T>
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

    public void Clear()
    {
        _semaphore.Wait();
        try
        {
            _cache.Clear();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Update(T entry)
    {
        var match = _cache.FirstOrDefault(x => x.Id == entry.Id);
        if (match != null)
        {
            _cache.Remove(match);
            _cache.Add(entry);
        }
    }

    public void Add(T entry)
    {
        _cache.Add(entry);
    }

    public void Delete(T entry)
    {
        _cache.Remove(entry);
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
