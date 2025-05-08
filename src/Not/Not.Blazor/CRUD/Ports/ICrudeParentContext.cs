using Not.Domain;
using Not.Injection;

namespace Not.Blazor.CRUD.Ports;

/// <summary>
/// Used in a loop without filtration. Implementations must implement type checking and NOT throw any errors
/// </summary>
public interface ICrudeParentContext : ISingleton
{
    /// <summary>
    /// Set the parent reference
    /// </summary>
    /// <param name="parent"></param>
    void Set(IParent parent);
}
