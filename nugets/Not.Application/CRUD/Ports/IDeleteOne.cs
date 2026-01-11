using System.Linq.Expressions;

namespace Not.Application.CRUD.Ports;

public interface IDelete<T>
{
    Task Delete(int id);
    Task Delete(Expression<Func<T, bool>> filter);
}
