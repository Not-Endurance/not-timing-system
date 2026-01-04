using Not.Concurrency.Extensions;

namespace Not.Concurrency.Extensions;

// TODO: This should be in its own namespace. Something like Not.Async or Not.Tasks
public static class TaskExtensions
{
    public static void ToVoid(this Task task)
    {
        task.ContinueWith(x => { });
    }

    public static void ToVoid<T>(this Task<T> task)
    {
        task.ContinueWith(x => { });
    }

    public static async Task<List<T>> ToList<T>(this Task<IEnumerable<T>> enumerableTask)
    {
        return (await enumerableTask).ToList();
    }

    public static async Task<IEnumerable<TOut>> Select<T, TOut>(
        this Task<IEnumerable<T>> enumerableTask,
        Func<T, TOut> selector
    )
    {
        var enumerable = await enumerableTask;
        return enumerable.Select(selector);
    }
}
