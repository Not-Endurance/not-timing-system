namespace Not.Async;

/// <summary>How long a retry waits before it goes on; a test steps through it.</summary>
public interface IDelay
{
    Task Wait(TimeSpan span, CancellationToken cancellationToken);
}
