namespace NC.Application.CRUD.Ports;

public interface IDelete<T>
{
    Task Delete(T item);
}
