namespace Not.Collections;

public static class CollectionExtensions
{
    public static void RemoveIfExisting<T>(this IList<T> list, Func<T, bool> predicate)
    {
        var existing = list.FirstOrDefault(predicate);
        if (existing != null)
        {
            list.Remove(existing);
        }
    }

    public static void Update<T>(this IList<T> list, T item, NCollectionAction action)
    {
        if (action == NCollectionAction.Remove)
        {
            list.Remove(item);
            return;
        }
        if (action == NCollectionAction.AddOrUpdate)
        {
            var match = list.FirstOrDefault(x => x?.Equals(item) == true);
            if (match == null)
            {
                list.Add(item);
                return;
            }
            var index = list.IndexOf(match);
            list.Remove(match);
            list.Insert(index, item);
            return;
        }
    }
}
