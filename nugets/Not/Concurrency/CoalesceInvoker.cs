namespace Not.Concurrency;

public class CoalesceInvoker
{
    readonly Func<Task> _action;
    readonly Gate _gate = new();
    int _isPending;

    public CoalesceInvoker(Func<Task> action)
    {
        _action = action;
    }

    public async Task Invoke()
    {
        Interlocked.Exchange(ref _isPending, 1);
        await RunActionLoop();
    }

    async Task RunActionLoop()
    {
        if (!_gate.EnterIfAvailable())
        {
            return;
        }
        try
        {
            while (Interlocked.Exchange(ref _isPending, 0) == 1)
            {
                await _action();
            }
        }
        finally
        {
            _gate.Exit();
        }
    }
}
