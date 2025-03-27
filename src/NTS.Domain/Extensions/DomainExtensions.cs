using Not.Domain.Base;
using Not.Exceptions;

namespace NTS.Domain.Extensions;

public static class DomainExtensions
{
    public static void Update<T>(this List<T> collection, T entity)
        where T : AggregateRoot
    {
        var index = collection.IndexOf(entity);
        if (index == -1)
        {
            throw GuardHelper.Exception("Empty collections cannot be updated");
        }
        collection.Remove(entity);
        collection.Insert(index, entity);
    }
}
