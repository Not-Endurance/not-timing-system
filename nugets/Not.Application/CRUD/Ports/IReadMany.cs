using System.Linq.Expressions;
using Not.Domain.Aggregates;
using Not.Injection;

namespace Not.Application.CRUD.Ports;

public interface IReadMany<T> : ITransient
    where T : IAggregateRoot
{
    Task<IEnumerable<T>> ReadAll();
    Task<IEnumerable<T>> ReadAll(Expression<Func<T, bool>> filter);
}
