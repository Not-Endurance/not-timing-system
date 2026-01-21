namespace Not.Concurrency;

public class Gate : IDisposable
{
    SemaphoreSlim _semaphore = new (1, 1);

    public bool EnterIfAvailable()
    {
        if (!_semaphore.Wait(0))
        {
            return false;
        }
        return true;
    }

    public void Dispose()
    {
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }

    public Task WaitToEnter()
    {
        return _semaphore.WaitAsync();
    }

    public void Exit()
    {
        _semaphore.Release();
    }
}
