namespace Not.Async;

/// <summary>Work that is tried again, after a wait, for as long as there is something to try.</summary>
public interface IRetryable
{
    /// <summary>Whether there is something to try again now.</summary>
    bool CanRetry { get; }

    /// <summary>
    /// Tries once. Returns true when nothing is left to try. The work reports its own failures: a failure that escapes it ends
    /// the retries.
    /// </summary>
    Task<bool> RetryAsync(CancellationToken cancellationToken);
}
