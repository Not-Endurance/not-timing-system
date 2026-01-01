using System.Linq.Expressions;
using Not.Domain.Aggregates;

namespace Not.Application.CRUD.Ports;

public interface IReadMany<T>
    where T : IAggregateRoot
{
    Task<IEnumerable<T>> ReadAll();
    Task<IEnumerable<T>> ReadAll(Expression<Func<T, bool>> filter);
}
