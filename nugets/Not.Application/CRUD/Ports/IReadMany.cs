using System.Linq.Expressions;

namespace Not.Application.CRUD.Ports;

public interface IReadMany<T>
{
    Task<IEnumerable<T>> ReadAll();
    Task<IEnumerable<T>> ReadAll(Expression<Func<T, bool>> filter);
}
