namespace NC.Application.CRUD.Ports;

public interface ICreate<in T>
{
    Task Create(T item);
}
