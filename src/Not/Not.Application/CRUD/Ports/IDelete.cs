using System.Linq.Expressions;
using Not.Domain;

namespace Not.Application.CRUD.Ports;

public interface IDelete<T>
    where T : IAggregateRoot
{
    Task Delete(int id);
    Task Delete(T item);
    Task Delete(Expression<Func<T, bool>> filter);
}
