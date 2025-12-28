using Not.Domain.Aggregates;
using Not.Injection;

namespace Not.Application.CRUD.Ports;

public interface IRepository<T> : ICreate<T>, IRead<T>, IUpdate<T>, IDelete<T>, IDeleteMany<T>, ITransient
    where T : IAggregateRoot { }
