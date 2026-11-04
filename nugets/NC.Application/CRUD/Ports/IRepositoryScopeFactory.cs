namespace NC.Application.CRUD.Ports;

public interface IRepositoryScopeFactory<T>
{
    public IRepositoryScope<T> Create();
}
