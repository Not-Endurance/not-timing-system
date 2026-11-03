namespace Not.Test;

/// <summary>
/// Waits for a condition with a deadline (ADR-0016): the condition is polled until it holds. When the deadline has passed,
/// the wait throws a <see cref="TimeoutException"/> that names what it was waiting for, so that a failing test says what
/// it waited for, not only that it waited.
/// </summary>
public static class DeadlineWait
{
    static readonly TimeSpan POLL = TimeSpan.FromMilliseconds(25);

    public static Task UntilAsync(string awaited, Func<bool> condition, TimeSpan deadline)
    {
        return UntilAsync(awaited, () => Task.FromResult(condition()), deadline);
    }

    public static async Task UntilAsync(string awaited, Func<Task<bool>> condition, TimeSpan deadline)
    {
        var stop = DateTimeOffset.UtcNow + deadline;
        while (!await condition())
        {
            if (DateTimeOffset.UtcNow >= stop)
            {
                throw new TimeoutException($"Timed out after {deadline} waiting for {awaited}.");
            }

            await Task.Delay(POLL);
        }
    }
}
