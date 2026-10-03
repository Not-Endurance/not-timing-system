using System.Linq.Expressions;

namespace Not.Application.CRUD.Ports;

public interface IRead<T>
{
    Task<T?> Read(Guid id);
    Task<T?> Read(Expression<Func<T, bool>> filter);
}
