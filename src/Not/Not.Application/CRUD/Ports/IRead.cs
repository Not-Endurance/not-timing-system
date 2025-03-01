using System.Linq.Expressions;
using Not.Domain;
using Not.Injection;

namespace Not.Application.CRUD.Ports;

public interface IRead<T> : ITransient
    where T : IAggregateRoot
{
    Task<T?> Read(Expression<Func<T, bool>> filter);
    Task<T?> Read(int id);
    Task<IEnumerable<T>> ReadAll();
    Task<IEnumerable<T>> ReadAll(Expression<Func<T, bool>> filter);
}
