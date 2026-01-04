namespace Not.Concurrency.Extensions;

public static class SemaphoreExtensions
{
    public static bool IsOpen(this SemaphoreSlim semaphore)
    {
        return semaphore.CurrentCount > 0;
    }

    public static bool IsClosed(this SemaphoreSlim semaphore)
    {
        return semaphore.CurrentCount == 0;
    }
}
