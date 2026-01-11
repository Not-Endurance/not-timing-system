using System.Linq.Expressions;

namespace Not.Application.CRUD.Ports;

public interface IDeleteOne<T>
{
    Task Delete(T item);
}
