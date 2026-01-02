using Not.Domain.Aggregates;

namespace NTS.Domain.Extensions;

public static class DomainExtensions
{
    public static void Update<T>(this List<T> collection, T entity)
        where T : AggregateRoot
    {
        var index = collection.IndexOf(entity);
        collection.Remove(entity);
        collection.Insert(index, entity);
    }
}
