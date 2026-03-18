using System.Linq.Expressions;

namespace Not.Application.CRUD.Ports;

public interface ISoftDelete<T> : IDelete<T>, IDeleteMany<T>
{
    Task HardDelete(T item);
    Task HardDelete(IEnumerable<T> items);
    Task HardDelete(Expression<Func<T, bool>> filter);
}
