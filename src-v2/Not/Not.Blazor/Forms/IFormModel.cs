namespace Not.Blazor.Ports;

public interface IFormModel<T>
{
    void FromEntity(T entity);
}