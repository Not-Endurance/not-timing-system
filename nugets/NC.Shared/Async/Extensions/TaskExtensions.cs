using System.Collections.Generic;
using System.Collections.ObjectModel;
using Not.Async.Extensions;

namespace Not.Async.Extensions;

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

    public static async Task<ReadOnlyCollection<T>> AsReadonly<T>(this Task<List<T>> task)
    {
        return (await task).ToList().AsReadOnly();
    }

    public static async Task<T?> FirstOrDefault<T>(this Task<IEnumerable<T>> task)
    {
        return (await task).FirstOrDefault();
    }

    public static async Task<T?> FirstOrDefault<T>(this Task<IEnumerable<T>> task, Func<T, bool> filter)
    {
        return (await task).FirstOrDefault(filter);
    }

    public static async Task<IReadOnlyList<T>> AsReadOnly<T>(this Task<IEnumerable<T>> task)
    {
        return (await task).ToList().AsReadOnly();
    }
}
