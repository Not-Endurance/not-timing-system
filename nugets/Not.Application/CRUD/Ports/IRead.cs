using System.Linq.Expressions;
using Not.Domain.Aggregates;

namespace Not.Application.CRUD.Ports;

public interface IRead<T>
{
    Task<T?> Read(Expression<Func<T, bool>> filter);
    Task<T?> Read(int id);
}
