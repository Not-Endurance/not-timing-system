using Not.Exceptions;

namespace Not.Async;

public class CoalesceInvoker
{
    readonly Gate _gate = new();
    Func<Task>? _action;
    int _isPending;

    public CoalesceInvoker()
    {
    }

    public CoalesceInvoker(Func<Task> action)
    {
        _action = action;
    }

    public async Task Invoke()
    {
        Interlocked.Exchange(ref _isPending, 1);
        await RunActionLoop();
    }

    public async Task Invoke(Func<Task> action)
    {
        _action = action;
        Interlocked.Exchange(ref _isPending, 1);
        await RunActionLoop();
    }

    async Task RunActionLoop()
    {
        if (!_gate.EnterIfOpen())
        {
            return;
        }
        try
        {
            while (Interlocked.Exchange(ref _isPending, 0) == 1)
            {
                GuardHelper.ThrowIfDefault(_action);
                await _action();
            }
        }
        finally
        {
            _gate.Exit();
        }
    }
}
