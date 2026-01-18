namespace Not.Application.Services;

public interface IFormModel<T>
{
    void FromEntity(T entity);
}
