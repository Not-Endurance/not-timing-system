namespace Not.Application.Services;

public interface IFormModel<T>
{
    int? Id { get; set; }
    void FromEntity(T entity);
}
