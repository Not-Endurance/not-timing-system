using Not.Application.CRUD.Ports;

namespace NTS.Application.Contracts.Core;

public interface IEventScopedRepository<T> : IRepository<T> { }
