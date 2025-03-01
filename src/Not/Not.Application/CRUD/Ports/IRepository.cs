using Not.Domain;

namespace Not.Application.CRUD.Ports;

public interface IRepository<T> : ICreate<T>, IRead<T>, IUpdate<T>, IDelete<T>
    where T : IAggregateRoot { }
