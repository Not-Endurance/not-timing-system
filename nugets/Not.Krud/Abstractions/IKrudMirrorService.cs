using Not.Domain.Abstractions;

namespace Not.Krud.Abstractions;

public interface IKrudMirrorService<in T>
    where T : IEntity
{
    Task Reflect(T entity);
}
