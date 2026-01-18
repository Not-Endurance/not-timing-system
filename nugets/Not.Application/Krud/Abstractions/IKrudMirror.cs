using Not.Domain.Aggregates;
using Not.Injection;

namespace Not.Application.Krud.Abstractions;

public interface IKrudMirror<in T> : ISingleton
    where T : IEntity
{
    Task Reflect(T entity);
}
