using Not.Concurrency.Extensions;
using Not.Logging;

namespace Not.Concurrency;

public class TimeoutLockSemaphore
{
    readonly SemaphoreSlim _semaphore = new(1);
    readonly TimeSpan _timout;
    Guid? _lockId;

    public TimeoutLockSemaphore(TimeSpan? timout = null)
    {
#if DEBUG
        _timout = TimeSpan.FromMinutes(1);
#else
        _timout = timout ?? TimeSpan.FromMilliseconds(500);
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

    async Task Timeout(Guid id, string callerFile, string callerMember)
    {
        await Task.Delay(_timout);
        if (_lockId != id || _semaphore.IsOpen())
        {
            return;
        }
        LoggingHelper.Error($"Syncronization timeout occured in {callerMember}, file {callerFile}");
        Release();
    }

    async Task<Guid> Lock()
    {
        await _semaphore.WaitAsync();
        _lockId = Guid.NewGuid();
        return _lockId.Value;
    }

    void Release()
    {
        _lockId = null;
        _semaphore.Release();
    }
}
