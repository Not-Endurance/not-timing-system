using System.Linq.Expressions;

namespace Not.Application.CRUD.Ports;

public interface IDeleteMany<T>
{
    Task DeleteMany(IEnumerable<T> items);
    Task DeleteMany(Expression<Func<T, bool>> filter);
}
