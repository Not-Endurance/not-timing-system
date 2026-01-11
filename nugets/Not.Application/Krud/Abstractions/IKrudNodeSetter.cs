using Not.Injection;

namespace Not.Application.Krud.Abstractions;

/// <summary>
/// Used in a loop without filtration. Implementations must implement type checking and NOT throw any errors
/// </summary>
public interface IKrudNodeSetter : ISingleton
{
    /// <summary>
    /// Set the parent reference
    /// </summary>
    /// <param name="nodeValue"></param>
    Task Set(object nodeValue);
}
