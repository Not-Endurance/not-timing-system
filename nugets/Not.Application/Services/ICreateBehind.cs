namespace Not.Application.Services;

// TODO: Remove and use IRepository instead
public interface ICreateBehind<T>
{
    Task Create(T model);
}
