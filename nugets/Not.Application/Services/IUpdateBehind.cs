namespace Not.Application.Services;

public interface IUpdateBehind<T>
{
    Task Update(T model);
}
