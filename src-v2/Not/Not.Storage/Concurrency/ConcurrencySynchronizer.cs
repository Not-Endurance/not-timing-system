using Not.Concurrency;
using Not.Logging;

namespace Not.Storage.Concurrency;

internal class ConcurrencySynchronizer
{
    private readonly static SemaphoreSlim _semaphore = new(1);
    private readonly TimeSpan _timout;
    private Guid? _lockId;

    public ConcurrencySynchronizer(TimeSpan? timout = null)
    {
#if DEBUG
        _timout = TimeSpan.FromMinutes(1);
#else
        _timout = timout ?? TimeSpan.FromMicroseconds(500);
#endif
    }

    public async Task<Guid> Wait(string callerPath, string callerMember)
    {
        var id = await Lock();
        Timeout(id, callerMember, callerPath).ToVoid();
        return id;
    }

    public void Release(Guid id)
    {
        if (_lockId != id)
        {
            return;
        }
        Release();
    }

    private async Task Timeout(Guid id, string callerFile, string callerMember)
    {
        await Task.Delay(_timout);
        if (_lockId != id || _semaphore.IsOpen())
        {
            return;
        }
        LoggingHelper.Error($"Syncronization timeout occured in {callerMember}, file {callerFile}");
        Release();
    }

    private async Task<Guid> Lock()
    {
        await _semaphore.WaitAsync();
        _lockId = Guid.NewGuid();
        return _lockId.Value;
    }

    private void Release()
    {
        _lockId = null;
        _semaphore.Release();
    }
}
