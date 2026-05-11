using System.Linq.Expressions;

namespace Not.Application.CRUD.Ports;

// TODO: Remove from implementations. Should be kept only as operation shape, but shouldn't be resovled from DI container
public interface IReadMany<T>
{
    Task<IEnumerable<T>> ReadMany();
    Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter);
}
