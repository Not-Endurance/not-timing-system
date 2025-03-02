using System.Linq.Expressions;

namespace Not.Async;

public static class TaskExtensions
{
    public static async Task<List<T>> ToList<T>(this Task<IEnumerable<T>> task)
    {
        return (await task).ToList();
    }

    public static async Task<T?> FirstOrDefault<T>(this Task<IEnumerable<T>> task, Func<T, bool> filter)
    {
        return (await task).FirstOrDefault(filter);
    }
}
