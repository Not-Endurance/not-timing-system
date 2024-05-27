using Not.Serialization;

namespace Not.Storage.Stores;

public class InMemoryWithJsonFileBackupStore<T> : IStore<T>
    where T : class, new()
{
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
    private readonly IFileBackupConfiguration _configuration;
    private T _state = new();

    public InMemoryWithJsonFileBackupStore(IFileBackupConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<T> Load()
    {
        await _semaphore.WaitAsync();
        return _state;
    }

    public async Task Commit(T state)
    {

        var json = _state.ToJson();
        await File.WriteAllTextAsync(_configuration.DataFilePath, json);
    }
}

public interface IFileBackupConfiguration
{
    string DataFilePath { get; }
}
