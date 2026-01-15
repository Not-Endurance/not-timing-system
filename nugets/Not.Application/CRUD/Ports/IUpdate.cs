namespace Not.Application.CRUD.Ports;

public interface IUpdate<in T>
{
    Task Update(T items);
}
