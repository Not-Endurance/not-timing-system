namespace NTS.Tests.Unit.Harness;

internal sealed class Held
{
    readonly List<TaskCompletionSource> _runs = [];
    int _started;

    public int Started => Volatile.Read(ref _started);

    public Task Run()
    {
        var run = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_runs)
        {
            _runs.Add(run);
        }

        Interlocked.Increment(ref _started);
        return run.Task;
    }

    public void Release(int index)
    {
        lock (_runs)
        {
            _runs[index].SetResult();
        }
    }
}
