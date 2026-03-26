using Not.Domain.Abstractions;

namespace Not.Krud.Abstractions;

public interface IKrudMirror<in T>
    where T : IEntity
{
    Task Reflect(T entity);
}
