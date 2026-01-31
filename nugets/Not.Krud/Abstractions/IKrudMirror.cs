using Not.Domain.Abstractions;
using Not.Injection;

namespace Not.Krud.Abstractions;

public interface IKrudMirror<in T> : ISingleton
    where T : IEntity
{
    Task Reflect(T entity);
}
