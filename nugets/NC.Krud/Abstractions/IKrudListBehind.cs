using NC.Application.Services;
using NC.Domain;
using NC.Krud.Models;

namespace NC.Krud.Abstractions;

public interface IKrudListBehind<T> : ISettService<T>
    where T : Entity
{
    Task<KrudDeleteImpact> PreviewDelete(T entity);
    Task DeleteCascade(T entity);
}
