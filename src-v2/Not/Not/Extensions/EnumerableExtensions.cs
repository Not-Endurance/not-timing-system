namespace Not.Extensions;

public static class EnumerableExtensions
{
    private const int NOT_FOUND = -1;

    public static T[] AsArray<T>(this T obj)
    {
        return new[] { obj };
    }

    public static int NumberOf<T>(this IEnumerable<T> enumerable, T item)
    {
        return NumberOf(enumerable.ToList(), item);
    }

    public static int NumberOf<T>(this IList<T> enumerable, T item)
    {
        var index = enumerable.IndexOf(item);
        return index == NOT_FOUND
            ? NOT_FOUND
            : index + 1;
    }

    public static IEnumerable<TElement> FlattenGroupedItems<TKey, TElement>(this
    IOrderedEnumerable<IGrouping<TKey, TElement>> groupedStarts)
    {
        foreach (var group in groupedStarts)
        {
            foreach (var groupedAndOrderedList in group)
            {
                yield return groupedAndOrderedList;
            }
        }
    }

}
