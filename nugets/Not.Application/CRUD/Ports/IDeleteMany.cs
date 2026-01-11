using System.Linq.Expressions;

namespace Not.Application.CRUD.Ports;

public interface IDeleteMany<T>
{
    Task Delete(IEnumerable<T> items);
    Task Delete(Expression<Func<T, bool>> filter);
}
