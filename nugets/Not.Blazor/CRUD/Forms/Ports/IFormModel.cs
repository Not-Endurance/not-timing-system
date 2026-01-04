namespace Not.Blazor.CRUD.Forms.Ports;

public interface IFormModel<T>
{
    int? Id { get; set; }
    void FromEntity(T entity);
}
