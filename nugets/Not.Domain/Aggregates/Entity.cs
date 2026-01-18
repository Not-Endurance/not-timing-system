using Not.Exceptions;
using Not.Extensions;

namespace Not.Domain.Aggregates;

public abstract class Entity : InLineEntityValidator, IEntity, IEquatable<Entity>
{
    const int PRIME = 7302013;

    public static bool operator ==(Entity? left, Entity? right)
    {
        return left?.Equals(right) ?? right is null;
    }

    public static bool operator !=(Entity? left, Entity? right)
    {
        return !(left == right);
    }

    readonly object?[] _equatableValues;

    protected Entity(object? equatableValue)
        : this([equatableValue]) { }

    protected Entity(object? equatableValue1, object? equatableValue2)
        : this([equatableValue1, equatableValue2]) { }

    protected Entity(object? equatableValue1, object? equatableValue2, object? equatableValue3)
        : this([equatableValue1, equatableValue2, equatableValue3]) { }

    protected Entity(object? equatableValue1, object? equatableValue2, object? equatableValue3, object? equatableValue4)
        : this([equatableValue1, equatableValue2, equatableValue3, equatableValue4]) { }

    protected Entity(object?[] equatableValues)
    {
        GuardHelper.ThrowIfEmpty(equatableValues);
        _equatableValues = equatableValues;
    }

    protected string Combine(params object?[] values)
    {
        return DomainModelHelper.Combine(values);
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

    public override int GetHashCode()
    {
        unchecked // Allows int to overflow without exceptions
        {
            var hashCode = 1430287;
            foreach (var value in _equatableValues.Where(x => x != null))
            {
                hashCode = hashCode * PRIME ^ value!.GetHashCode();
            }
            return hashCode;
        }
    }

    bool IsEqual(object? other)
    {
        if (other is null or not Entity)
        {
            return false;
        }
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        return GetHashCode() == other.GetHashCode() && GetType() == other.GetType();
    }
}
