using System.Linq.Expressions;
using Not.Domain.Aggregates;
using Not.Injection;

namespace Not.Application.CRUD.Ports;

public interface IDelete<T> : ITransient
    where T : IAggregateRoot
{
    Task Delete(int id);
    Task Delete(T item);
    Task Delete(Expression<Func<T, bool>> filter);
}
