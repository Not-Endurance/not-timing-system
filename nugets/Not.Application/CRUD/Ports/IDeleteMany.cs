namespace Not.Application.CRUD.Ports;

public interface IDeleteMany<in T>
{
    Task Delete(params IEnumerable<T> items);
}
