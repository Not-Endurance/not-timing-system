using System.Linq.Expressions;

namespace NC.Application.CRUD.Ports;

public interface IRead<T>
{
    Task<T?> Read(int id);
    Task<T?> Read(Expression<Func<T, bool>> filter);
}
