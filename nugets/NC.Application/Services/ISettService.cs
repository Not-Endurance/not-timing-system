namespace Not.Application.Services;

public interface ISettService<T>
{
    Task Delete(T entity);
    Task<IEnumerable<T>> ReadMany();
}
