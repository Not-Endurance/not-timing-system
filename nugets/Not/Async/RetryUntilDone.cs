namespace Not.Async;

/// <summary>
/// Tries work again and again: it waits, starting at <paramref name="first"/> and doubling up to <paramref name="longest"/>,
/// then tries once, for as long as the work can be retried and until the work reports that nothing is left. A cancellation
/// ends the loop, and so does a failure that escapes the work.
/// </summary>
public static class RetryUntilDone
{
    public static async Task Run(
        IDelay delay,
        IRetryable work,
        TimeSpan first,
        TimeSpan longest,
        CancellationToken stop
    )
    {
        var wait = first;
        while (!stop.IsCancellationRequested && work.CanRetry)
        {
            try
            {
                await delay.Wait(wait, stop);
                wait = TimeSpan.FromTicks(Math.Min(wait.Ticks * 2, longest.Ticks));
                if (await work.RetryAsync(stop))
                {
                    return;
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
