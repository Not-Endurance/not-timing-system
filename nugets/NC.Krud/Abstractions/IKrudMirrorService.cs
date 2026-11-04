using NC.Domain.Abstractions;

namespace NC.Krud.Abstractions;

public interface IKrudMirrorService<in T>
    where T : IEntity
{
    Task Reflect(T entity);
}
