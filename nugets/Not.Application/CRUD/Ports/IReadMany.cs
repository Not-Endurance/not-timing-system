using System.Linq.Expressions;

namespace Not.Application.CRUD.Ports;

public interface IReadMany<T>
{
    Task<IEnumerable<T>> ReadMany();
    Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter);
}
