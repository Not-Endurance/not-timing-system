namespace Not.Concurrency;

public class Gate : IDisposable
{
    const int MAX_COUNT = 1;

    SemaphoreSlim _semaphore = new(1, MAX_COUNT);

    public bool IsOpen => _semaphore.CurrentCount == MAX_COUNT;

    public bool EnterIfOpen()
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
