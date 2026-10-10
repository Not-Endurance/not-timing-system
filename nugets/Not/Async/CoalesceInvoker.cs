using Not.Exceptions;

namespace Not.Async;

public class CoalesceInvoker
{
    readonly Gate _gate = new();
    Func<Task>? _action;
    int _isPending;
    Task _running = Task.CompletedTask;

    public CoalesceInvoker() { }

    public CoalesceInvoker(Func<Task> action)
    {
        _action = action;
    }

    public async Task Invoke()
    {
        Interlocked.Exchange(ref _isPending, 1);
        await RunActionLoop();
    }

    /// <summary>
    /// Completes when the run that is under way has done everything it was asked for, which is how a caller that needs what
    /// the action writes waits for it: completed already when nothing runs. It does not say whether the action failed.
    /// </summary>
    public Task WhenIdle()
    {
        return Volatile.Read(ref _running);
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
        var finished = new TaskCompletionSource();
        Volatile.Write(ref _running, finished.Task);
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
            finished.SetResult();
        }
    }
}
