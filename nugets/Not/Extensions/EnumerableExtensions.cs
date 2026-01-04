namespace Not.Extensions;

public static class EnumerableExtensions
{
    const int NOT_FOUND = -1;

    public static T[] AsArray<T>(this T obj)
    {
        return [obj];
    }

    public static int NumberOf<T>(this IEnumerable<T> enumerable, T item)
    {
        var list = enumerable.ToList();
        return NumberOf(list, item);
    }

    public static int NumberOf<T>(this IList<T> enumerable, T item)
    {
        var index = enumerable.IndexOf(item);
        return index == NOT_FOUND ? NOT_FOUND : index + 1;
    }
}
