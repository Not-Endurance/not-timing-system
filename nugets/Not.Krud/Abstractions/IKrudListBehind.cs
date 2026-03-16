using Not.Application.Services;
using Not.Domain;
using Not.Krud.Models;

namespace Not.Krud.Abstractions;

public interface IKrudListBehind<T> : IListBehind<T>
    where T : Entity
{
    Task<KrudDeleteImpact> PreviewDelete(T entity);
    Task DeleteCascade(T entity);
}
