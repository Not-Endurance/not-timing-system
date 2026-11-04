using NC.Domain;
using NC.Krud.Models;

namespace NC.Krud.Services;

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
