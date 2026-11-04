using System.Linq.Expressions;

namespace Not.Application.CRUD.Ports;

public interface IRepositoryScope<T>
{
    Expression<Func<T, bool>> Filter { get; }
}
