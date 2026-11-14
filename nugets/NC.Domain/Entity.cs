using NC.Domain.Abstractions;
using NC.Domain.Exceptions;
using NC.Extensions;
using NC.Objects;

namespace NC.Domain;

public abstract class Entity : InLineEntityValidator, IEntity, IEquatable<Entity>
{
    /// <summary>
    /// Provide <paramref name="id"/> when updating state null to generate it
    /// </summary>
    /// <param name="id">Id, generated when null</param>
    protected Entity(int? id, int domainVersion = 1, int? storedDomainVersion = null)
    {
        Id = id ?? DomainModelHelper.GenerateId();
        DomainVersion = domainVersion;
        EnsureStoredVersionMatches(storedDomainVersion);
    }

    /// <summary>
    /// The version of the domain rules this entity was built with. It is stored with the document, so a document
    /// written by other rules is refused when it is loaded.
    /// </summary>
    public int DomainVersion { get; }

    public int Id { get; }

    protected string Combine(params object?[] values)
    {
        return DomainModelHelper.Combine(values);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public bool Equals(Entity? other)
    {
        return IsEqual(other);
    }

    public override bool Equals(object? other)
    {
        return IsEqual(other);
    }

    public override string ToString()
    {
        throw new NotImplementedException(
            $"'{GetType().Name}' is '{nameof(Entity)}' and is required to override ToString() to provide short info"
        );
    }

    void EnsureStoredVersionMatches(int? storedDomainVersion)
    {
        if (storedDomainVersion == null || storedDomainVersion == DomainVersion)
        {
            return;
        }
        throw new DomainException(
            $"{GetType().Name}: stored {storedDomainVersion}, code {DomainVersion}, delta {storedDomainVersion - DomainVersion}"
        );
    }

    bool IsEqual(object? other)
    {
        if (other is null or not Entity)
        {
            return false;
        }
        return ObjectHelper.AreEqual(this, other);
    }

    public static bool operator ==(Entity? left, Entity? right)
    {
        return left?.Equals(right) ?? right is null;
    }

    public static bool operator !=(Entity? left, Entity? right)
    {
        return !(left == right);
    }
}
