namespace Not.Domain.Krud;

/// <summary>
/// Implementations of this interface are expected to reflect changes ot <typeparamref name="T"/> that occur separately
/// </summary>
/// <typeparam name="T">Type of reflection</typeparam>
public interface IEntityMirror<T>
{
    void Reflect(T reflection);
}
