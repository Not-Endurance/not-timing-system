namespace Not.Async;

/// <summary>
/// Runs one action at a time for each key, and folds the calls that arrive meanwhile into exactly one more run, after
/// the current one. Every caller gets the task of the run it was folded into: it completes when the run for its key is
/// quiet, so a caller that awaits it knows that a run which started after its call has finished, and a failure reaches
/// every caller that waited. Whether a call arrives in time for the loop to see it is decided under the same lock as the
/// end of the loop, so no call is lost. The action of the latest call is the one the next run uses. The first run
/// starts on the thread of the caller, up to its first await. For one action, and callers that need not wait, see
/// <see cref="CoalesceInvoker"/>.
/// </summary>
public class CoalesceInvoker<TKey>
    where TKey : notnull
{
    readonly object _lock = new();
    readonly Dictionary<TKey, Run> _runs = [];

    public Task Invoke(TKey key, Func<Task> action)
    {
        Run run;
        lock (_lock)
        {
            if (_runs.TryGetValue(key, out var inFlight))
            {
                inFlight.Action = action;
                inFlight.RunAgain = true;
                return inFlight.Finished.Task;
            }

            run = new Run(action);
            _runs[key] = run;
        }

        _ = RunUntilQuiet(key, run);
        return run.Finished.Task;
    }

    async Task RunUntilQuiet(TKey key, Run run)
    {
        try
        {
            while (true)
            {
                Func<Task> action;
                lock (_lock)
                {
                    action = run.Action;
                }

                await action();
                lock (_lock)
                {
                    if (!run.RunAgain)
                    {
                        _runs.Remove(key);
                        break;
                    }

                    run.RunAgain = false;
                }
            }

            run.Finished.SetResult();
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _runs.Remove(key);
            }

            run.Finished.SetException(ex);
        }
    }

    sealed class Run
    {
        public Run(Func<Task> action)
        {
            Action = action;
        }

        public Func<Task> Action { get; set; }
        public bool RunAgain { get; set; }
        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
