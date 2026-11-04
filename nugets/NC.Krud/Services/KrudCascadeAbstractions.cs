using Not.Domain;
using Not.Krud.Models;

namespace Not.Krud.Services;

public interface IKrudCascadeRepository<T>
    where T : Entity
{
    Task<KrudDeleteImpact> PreviewDelete(T entity);
    Task DeleteCascade(T entity);
}

public interface IKrudDependencyResolver
{
    bool Supports(Type principalType);
    KrudDeleteImpact PreviewDelete(Entity principal);
    void CascadeDeleteDependencies(Entity principal);
}
