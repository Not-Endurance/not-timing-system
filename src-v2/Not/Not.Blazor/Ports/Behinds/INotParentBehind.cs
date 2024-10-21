using Not.Domain;
using Not.Injection;

namespace Not.Blazor.Ports.Behinds;

/// <summary>
/// <seealso cref="INotParentBehind{T}"/> represents a <seealso cref="INotBehind"/> that belongs to a tree hierarchy of <seealso cref="DomainEntity"/> objects
/// (see <seealso cref="IParent{T}"/> for more). Details regarding it's parent entity are responsiblity of the implementation
/// </summary>
/// <typeparam name="T">Type of parent entity</typeparam>
public interface INotParentBehind<T>: ISingletonService
{
    Task<T?> Initialize(int id);
}
