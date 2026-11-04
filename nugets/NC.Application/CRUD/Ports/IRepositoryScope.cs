using System.Linq.Expressions;

namespace NC.Application.CRUD.Ports;

public interface IRepositoryScope<T>
{
    Expression<Func<T, bool>> Filter { get; }
}
