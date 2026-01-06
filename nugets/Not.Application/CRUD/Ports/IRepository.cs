namespace Not.Application.CRUD.Ports;

public interface IRepository<T> : ICreate<T>, IRead<T>, IReadMany<T>, IUpdate<T>, IDelete<T>, IDeleteMany<T>
{ }
