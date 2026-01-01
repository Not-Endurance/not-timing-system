using System.Linq.Expressions;
using Not.Domain.Aggregates;
using Not.Injection;

namespace Not.Application.CRUD.Ports;

public interface IRead<T> : ITransient
    where T : IAggregateRoot
{
    Task<T?> Read(Expression<Func<T, bool>> filter);
    Task<T?> Read(int id);
}
