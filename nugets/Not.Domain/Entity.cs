using Not.Domain.Abstractions;
using Not.Extensions;

namespace Not.Domain;

public abstract class Entity : InLineEntityValidator, IEntity, IEquatable<Entity>
{
    /// <summary>
    /// Provide <paramref name="id"/> when updating state null to generate it
    /// </summary>
    /// <param name="id">Id, generated when null</param>
    protected Entity(Guid? id)
    {
        Id = id ?? Guid.NewGuid();
    }

    public Guid Id { get; }

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

    bool IsEqual(object? other)
    {
        return other is Entity entity && entity.GetType() == GetType() && entity.Id == Id;
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
