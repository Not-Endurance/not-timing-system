namespace Not.Concurrency;

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
}
