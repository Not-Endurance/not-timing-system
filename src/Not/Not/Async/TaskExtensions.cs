namespace Not.Async;

public static class TaskExtensions
{
    public static async Task<List<T>> ToList<T>(this Task<IEnumerable<T>> task)
    {
        return (await task).ToList();
    }
}
